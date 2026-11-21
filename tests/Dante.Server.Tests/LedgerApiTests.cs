using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dante.Banks;
using Dante.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dante.Server.Tests;

[Collection("api")]
public sealed class LedgerApiTests(DanteApiFactory factory)
{
    [Fact]
    public async Task Insert_stores_a_balanced_entry_in_utc()
    {
        var bank = await SeedBankAsync();
        var owner = await RegisterAsync();
        var debit = await CreateAccountAsync(owner.AccessToken, bank.Id, "Checking");
        var credit = await CreateAccountAsync(owner.AccessToken, bank.Id, "Savings");
        var occurredAt = new DateTimeOffset(2026, 3, 15, 22, 30, 0, TimeSpan.FromHours(-3));

        using var client = Authorized(owner.AccessToken);
        var response = await client.PostAsJsonAsync("/api/ledger", new
        {
            debitAccountId = debit,
            creditAccountId = credit,
            amount = 10.5m,
            occurredAt,
            timeZone = "America/Sao_Paulo",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = await ReadEntry(response);
        Assert.EndsWith($"/api/ledger/{entry.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(debit, entry.DebitAccountId);
        Assert.Equal(credit, entry.CreditAccountId);
        Assert.Equal(10.5m, entry.Amount);
        Assert.Equal(occurredAt.ToUniversalTime(), entry.OccurredAt);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
        Assert.Equal("America/Sao_Paulo", entry.TimeZone);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DanteDbContext>();
        var stored = await db.LedgerEntries.SingleAsync(candidate => candidate.Id == entry.Id);
        Assert.Equal(entry.OccurredAt, stored.OccurredAt);
    }

    [Fact]
    public async Task Insert_rejects_an_entry_that_does_not_balance()
    {
        var bank = await SeedBankAsync();
        var owner = await RegisterAsync();
        var other = await RegisterAsync();
        var account = await CreateAccountAsync(owner.AccessToken, bank.Id, "Checking");
        var foreign = await CreateAccountAsync(other.AccessToken, bank.Id, "Other");
        using var client = Authorized(owner.AccessToken);
        var when = DateTimeOffset.UtcNow;

        var sameAccount = await client.PostAsJsonAsync("/api/ledger", Entry(account, account, 10, when));
        Assert.Equal(HttpStatusCode.BadRequest, sameAccount.StatusCode);
        Assert.Equal("The debit and credit accounts must be different.", (await sameAccount.Content.ReadFromJsonAsync<ErrorBody>())?.Error);

        var missing = await client.PostAsJsonAsync("/api/ledger", Entry(account, Guid.NewGuid(), 10, when));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var someoneElse = await client.PostAsJsonAsync("/api/ledger", Entry(account, foreign, 10, when));
        Assert.Equal(HttpStatusCode.NotFound, someoneElse.StatusCode);

        var savings = await CreateAccountAsync(owner.AccessToken, bank.Id, "Savings");
        var zero = await client.PostAsJsonAsync("/api/ledger", Entry(account, savings, 0, when));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal("Amount must be greater than zero.", (await zero.Content.ReadFromJsonAsync<ErrorBody>())?.Error);

        var negative = await client.PostAsJsonAsync("/api/ledger", Entry(account, savings, -1, when));
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal("Amount must be greater than zero.", (await negative.Content.ReadFromJsonAsync<ErrorBody>())?.Error);

        var cents = await client.PostAsJsonAsync("/api/ledger", Entry(account, savings, 1.005m, when));
        Assert.Equal(HttpStatusCode.BadRequest, cents.StatusCode);
        Assert.Equal("Amount must have at most two decimal places.", (await cents.Content.ReadFromJsonAsync<ErrorBody>())?.Error);

        var zone = await client.PostAsJsonAsync("/api/ledger", new
        {
            debitAccountId = account,
            creditAccountId = savings,
            amount = 1,
            occurredAt = when,
            timeZone = "Not/AZone",
        });
        Assert.Equal(HttpStatusCode.BadRequest, zone.StatusCode);
        Assert.Equal("Time zone was not recognized.", (await zone.Content.ReadFromJsonAsync<ErrorBody>())?.Error);
    }

    [Fact]
    public async Task Deleting_an_account_deletes_entries_that_use_it()
    {
        var bank = await SeedBankAsync();
        var owner = await RegisterAsync();
        var debit = await CreateAccountAsync(owner.AccessToken, bank.Id, "Checking");
        var credit = await CreateAccountAsync(owner.AccessToken, bank.Id, "Savings");
        using var client = Authorized(owner.AccessToken);
        var created = await client.PostAsJsonAsync("/api/ledger", new
        {
            debitAccountId = debit,
            creditAccountId = credit,
            amount = 3,
            occurredAt = DateTimeOffset.UtcNow,
            timeZone = "America/Sao_Paulo",
        });
        var entry = await ReadEntry(created);

        var deleted = await client.DeleteAsync($"/api/accounts/{debit}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DanteDbContext>();
        Assert.False(await db.LedgerEntries.AnyAsync(candidate => candidate.Id == entry.Id));
    }

    [Fact]
    public async Task Search_converts_a_local_window_and_narrows_as_filters_are_added()
    {
        var bank = await SeedBankAsync();
        var owner = await RegisterAsync();
        var checking = await CreateAccountAsync(owner.AccessToken, bank.Id, "Checking");
        var savings = await CreateAccountAsync(owner.AccessToken, bank.Id, "Savings");
        var cash = await CreateAccountAsync(owner.AccessToken, bank.Id, "Cash");
        await PostEntry(owner.AccessToken, checking, savings, 10, new DateTimeOffset(2026, 3, 15, 22, 30, 0, TimeSpan.FromHours(-3)));
        await PostEntry(owner.AccessToken, checking, cash, 4, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.FromHours(-3)));

        using var client = Authorized(owner.AccessToken);
        var saoPaulo = await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["from"] = "2026-03-15T00:00:00",
            ["to"] = "2026-03-16T00:00:00",
            ["timeZone"] = "America/Sao_Paulo",
        }));
        Assert.Equal(HttpStatusCode.OK, saoPaulo.StatusCode);
        var inBrazil = await ReadPage(saoPaulo);
        Assert.Equal(10, Assert.Single(inBrazil.Entries).Amount);

        var utc = await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["from"] = "2026-03-15T00:00:00",
            ["to"] = "2026-03-16T00:00:00",
            ["timeZone"] = "UTC",
        }));
        Assert.Empty((await ReadPage(utc)).Entries);

        var byDebit = await ReadPage(await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["debitAccountId"] = checking.ToString(),
        })));
        Assert.Equal(2, byDebit.Entries.Count);

        var narrowed = await ReadPage(await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["debitAccountId"] = checking.ToString(),
            ["creditAccountId"] = savings.ToString(),
        })));
        Assert.Equal(10, Assert.Single(narrowed.Entries).Amount);

        var missing = await client.GetAsync("/api/ledger");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(
            "A debit account, a credit account, or a time window is required.",
            (await missing.Content.ReadFromJsonAsync<ErrorBody>())?.Error);
    }

    [Fact]
    public async Task Search_pages_by_occurred_at_and_rejects_a_cursor_from_another_search()
    {
        var bank = await SeedBankAsync();
        var owner = await RegisterAsync();
        var checking = await CreateAccountAsync(owner.AccessToken, bank.Id, "Checking");
        var savings = await CreateAccountAsync(owner.AccessToken, bank.Id, "Savings");
        await PostEntry(owner.AccessToken, checking, savings, 1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await PostEntry(owner.AccessToken, checking, savings, 2, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        await PostEntry(owner.AccessToken, checking, savings, 3, new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero));

        using var client = Authorized(owner.AccessToken);
        var first = await ReadPage(await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["debitAccountId"] = checking.ToString(),
            ["limit"] = "2",
        })));
        Assert.Equal([1, 2], first.Entries.Select(entry => entry.Amount));
        Assert.False(string.IsNullOrWhiteSpace(first.NextCursor));

        var second = await ReadPage(await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["debitAccountId"] = checking.ToString(),
            ["limit"] = "2",
            ["cursor"] = first.NextCursor,
        })));
        Assert.Equal(3, Assert.Single(second.Entries).Amount);
        Assert.Null(second.NextCursor);

        var changed = await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["creditAccountId"] = savings.ToString(),
            ["cursor"] = first.NextCursor,
        }));
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Equal(
            "The cursor belongs to a different search.",
            (await changed.Content.ReadFromJsonAsync<ErrorBody>())?.Error);
    }

    [Fact]
    public async Task Search_uses_the_earlier_offset_when_a_local_time_is_ambiguous()
    {
        var bank = await SeedBankAsync();
        var owner = await RegisterAsync();
        var checking = await CreateAccountAsync(owner.AccessToken, bank.Id, "Checking");
        var savings = await CreateAccountAsync(owner.AccessToken, bank.Id, "Savings");
        await PostEntry(owner.AccessToken, checking, savings, 5, new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero));
        await PostEntry(owner.AccessToken, checking, savings, 6, new DateTimeOffset(2026, 11, 1, 5, 29, 0, TimeSpan.Zero));

        using var client = Authorized(owner.AccessToken);
        var page = await ReadPage(await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["from"] = "2026-11-01T01:30:00",
            ["to"] = "2026-11-01T03:00:00",
            ["timeZone"] = "America/New_York",
        })));

        Assert.Equal(5, Assert.Single(page.Entries).Amount);

        var gap = await client.GetAsync(SearchUrl(new Dictionary<string, string?>
        {
            ["from"] = "2026-03-08T02:30:00",
            ["timeZone"] = "America/New_York",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, gap.StatusCode);
        Assert.Equal(
            "The local time does not exist in this time zone.",
            (await gap.Content.ReadFromJsonAsync<ErrorBody>())?.Error);
    }

    private async Task PostEntry(string accessToken, Guid debit, Guid credit, decimal amount, DateTimeOffset occurredAt)
    {
        using var client = Authorized(accessToken);
        var response = await client.PostAsJsonAsync("/api/ledger", new
        {
            debitAccountId = debit,
            creditAccountId = credit,
            amount,
            occurredAt,
            timeZone = "America/Sao_Paulo",
        });
        response.EnsureSuccessStatusCode();
    }

    private static string SearchUrl(Dictionary<string, string?> query)
    {
        var pairs = query
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}");
        return "/api/ledger?" + string.Join("&", pairs);
    }

    private static async Task<LedgerPageBody> ReadPage(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return System.Text.Json.JsonSerializer.Deserialize<LedgerPageBody>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    private static object Entry(Guid debit, Guid credit, decimal amount, DateTimeOffset occurredAt) => new
    {
        debitAccountId = debit,
        creditAccountId = credit,
        amount,
        occurredAt,
        timeZone = "America/Sao_Paulo",
    };

    private HttpClient Authorized(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<Guid> CreateAccountAsync(string accessToken, Guid bankId, string name)
    {
        using var client = Authorized(accessToken);
        var response = await client.PostAsJsonAsync("/api/accounts", new { bankId, name });
        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<AccountBody>();
        return account!.Id;
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
            LogoUrl = null,
        };
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        return bank;
    }

    private async Task<LoginBody> RegisterAsync()
    {
        var username = $"user{Guid.NewGuid():N}"[..12];
        using var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "correct-horse" });
        register.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "correct-horse" });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<LoginBody>())!;
    }

    private static async Task<LedgerBody> ReadEntry(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<LedgerBody>())!;
    }

    private sealed record LoginBody(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record AccountBody(Guid Id, string Name);

    private sealed record LedgerBody(
        Guid Id,
        Guid DebitAccountId,
        Guid CreditAccountId,
        decimal Amount,
        DateTimeOffset OccurredAt,
        string TimeZone);

    private sealed record LedgerPageBody(List<LedgerBody> Entries, string? NextCursor);

    private sealed record ErrorBody(string Error);
}
