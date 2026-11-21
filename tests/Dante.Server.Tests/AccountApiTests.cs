using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dante.Banks;
using Dante.Infrastructure;
using Dante.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dante.Server.Tests;

[Collection("api")]
public sealed class AccountApiTests(DanteApiFactory factory)
{
    [Fact]
    public async Task Register_stores_a_password_hash_and_login_returns_a_token()
    {
        var username = UniqueUsername();
        const string password = "correct-horse";
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DanteDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Username == username);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.DoesNotContain(password, user.PasswordHash);
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher<User>>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, password));

        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrWhiteSpace(token?.AccessToken));

        var rejected = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Fact]
    public async Task Accounts_belong_to_the_authenticated_user()
    {
        var bank = await SeedBankAsync();
        var owner = await RegisterAsync("owner-password");
        var other = await RegisterAsync("other-password");

        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        var created = await ownerClient.PostAsJsonAsync("/api/accounts", new { bankId = bank.Id, name = "Salary" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = await created.Content.ReadFromJsonAsync<AccountBody>();
        Assert.Equal("Salary", account?.Name);
        Assert.Equal(bank.Id, account?.Bank.Id);

        var ownerAccounts = await ownerClient.GetFromJsonAsync<List<AccountBody>>("/api/accounts");
        Assert.Equal(account!.Id, Assert.Single(ownerAccounts!).Id);

        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);
        var otherAccounts = await otherClient.GetFromJsonAsync<List<AccountBody>>("/api/accounts");
        Assert.Empty(otherAccounts!);

        var deleted = await ownerClient.DeleteAsync($"/api/accounts/{account.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var hidden = await otherClient.DeleteAsync($"/api/accounts/{account.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var remaining = await ownerClient.GetFromJsonAsync<List<AccountBody>>("/api/accounts");
        Assert.Empty(remaining!);

        var banks = await ownerClient.GetFromJsonAsync<List<BankBody>>("/api/banks");
        Assert.Contains(banks!, candidate => candidate.Id == bank.Id);
    }

    [Fact]
    public async Task Incoming_request_continues_the_callers_trace()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        var started = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = started.Add,
        };
        ActivitySource.AddActivityListener(listener);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("traceparent", $"00-{traceId}-{parentSpanId}-01");
        await client.GetAsync("/api/banks");

        var continued = started.SingleOrDefault(activity => activity.TraceId == traceId && activity.ParentSpanId == parentSpanId);
        Assert.True(continued is not null, string.Join(Environment.NewLine, started.Select(activity => $"{activity.Source.Name} {activity.DisplayName} {activity.TraceId} parent={activity.ParentSpanId}")));
    }

    [Fact]
    public async Task Duplicate_usernames_are_rejected_regardless_of_case()
    {
        var username = UniqueUsername();
        var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "first-password" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = username.ToUpperInvariant(), password = "second-password" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private async Task<Bank> SeedBankAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DanteDbContext>();
        var bank = new Bank
        {
            Id = Guid.NewGuid(),
            Ispb = Guid.NewGuid().ToString("N")[..8],
            Code = 1,
            FullName = "Banco do Brasil S.A.",
            ShortName = "Banco do Brasil",
            LogoUrl = "https://cdn.example/bb.svg",
        };
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        return bank;
    }

    private async Task<LoginBody> RegisterAsync(string password)
    {
        var username = UniqueUsername();
        using var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        register.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<LoginBody>())!;
    }

    private static string UniqueUsername() => $"user{Guid.NewGuid():N}"[..12];

    private sealed record LoginBody(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record AccountBody(Guid Id, string Name, BankBody Bank);

    private sealed record BankBody(Guid Id, string Ispb, int? Code, string FullName, string ShortName, string? LogoUrl);
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<DanteApiFactory>;
