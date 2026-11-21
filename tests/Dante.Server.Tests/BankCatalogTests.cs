using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Dante.Banks;
using Dante.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dante.Server.Tests;

public sealed class BankCatalogTests
{
    [Fact]
    public async Task Repository_maps_ispb_code_names_and_logo()
    {
        const string json = """
            [
              {"ispb":"1","name":"BCO DO BRASIL S.A.","code":1,"fullName":"Banco do Brasil S.A.","logo_url":"https://cdn.example/bb.svg"},
              {"ispb":"00038121","name":"Selic","code":null,"fullName":"Banco Central do Brasil - Selic","logo_url":null},
              {"ispb":"1","name":"duplicate","code":2,"fullName":"Duplicate","logo_url":null}
            ]
            """;

        using var client = new HttpClient(new StubHandler(json))
        {
            BaseAddress = new Uri("https://brasilapi.com.br/api/"),
        };
        var catalog = new BrasilApiBankCatalogRepository(client);

        var banks = await catalog.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, banks.Count);
        var brasil = Assert.Single(banks, bank => bank.Ispb == "00000001");
        Assert.Equal(1, brasil.Code);
        Assert.Equal("Banco do Brasil S.A.", brasil.FullName);
        Assert.Equal("BCO DO BRASIL S.A.", brasil.ShortName);
        Assert.Equal("https://cdn.example/bb.svg", brasil.LogoUrl);
        var selic = Assert.Single(banks, bank => bank.Ispb == "00038121");
        Assert.Null(selic.Code);
        Assert.Null(selic.LogoUrl);
    }

    [Fact]
    public async Task Sync_inserts_new_banks_updates_existing_ones_and_keeps_missing_ones()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var db = database.Db;
        db.Banks.Add(new Bank
        {
            Id = Guid.NewGuid(),
            Ispb = "00000000",
            Code = 1,
            FullName = "Old full name",
            ShortName = "Old short name",
            LogoUrl = "https://cdn.example/old.svg",
        });
        db.Banks.Add(new Bank
        {
            Id = Guid.NewGuid(),
            Ispb = "99999999",
            Code = 999,
            FullName = "Kept Bank",
            ShortName = "Kept",
        });
        await db.SaveChangesAsync();

        var synchronizer = new BankCatalogSynchronizer(new BankRepository(db));
        var result = await synchronizer.SyncAsync(
            [
                new RemoteBank("00000000", 1, "Banco do Brasil S.A.", "BCO DO BRASIL S.A.", "https://cdn.example/new.svg"),
                new RemoteBank("00000208", 70, "BRB - BANCO DE BRASILIA S.A.", "BRB", null),
            ],
            CancellationToken.None);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);

        var banks = await db.Banks.OrderBy(bank => bank.Ispb).ToListAsync();
        Assert.Equal(3, banks.Count);
        Assert.Equal("Banco do Brasil S.A.", banks[0].FullName);
        Assert.Equal("https://cdn.example/new.svg", banks[0].LogoUrl);
        Assert.Equal("BRB", banks[1].ShortName);
        Assert.Equal("Kept Bank", banks[2].FullName);
    }

    [Fact]
    public async Task Outgoing_http_call_propagates_the_active_trace()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is DanteTelemetry.ActivitySourceName or "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var listenerHost = new HttpListener();
        var port = FreePort();
        listenerHost.Prefixes.Add($"http://127.0.0.1:{port}/");
        listenerHost.Start();
        try
        {
            var received = Task.Run(async () =>
            {
                var context = await listenerHost.GetContextAsync();
                var header = context.Request.Headers["traceparent"];
                context.Response.StatusCode = 204;
                context.Response.Close();
                return header;
            });
            using var incoming = DanteTelemetry.ActivitySource.StartActivity("GET /api/banks");
            Assert.NotNull(incoming);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            await client.GetAsync($"http://127.0.0.1:{port}/banks/v1");

            var traceparent = await received;

            Assert.NotNull(traceparent);
            Assert.Contains(incoming.TraceId.ToHexString(), traceparent, StringComparison.OrdinalIgnoreCase);
            var propagatedSpanId = traceparent.Split('-')[2];
            Assert.NotEqual(incoming.SpanId.ToHexString(), propagatedSpanId);
        }
        finally
        {
            listenerHost.Close();
        }
    }

    private static int FreePort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        return ((IPEndPoint)tcp.LocalEndpoint).Port;
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
