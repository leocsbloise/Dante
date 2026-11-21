using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dante.Banks;

namespace Dante.Infrastructure;

public sealed class BrasilApiBankCatalogRepository(HttpClient httpClient) : IBankCatalogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<RemoteBank>> GetAllAsync(CancellationToken cancellationToken)
    {
        var payload = await httpClient.GetFromJsonAsync<List<BrasilApiBank>>("banks/v1", JsonOptions, cancellationToken);
        if (payload is null || payload.Count == 0)
            return [];

        var banks = new Dictionary<string, RemoteBank>(StringComparer.Ordinal);
        foreach (var bank in payload)
        {
            if (string.IsNullOrWhiteSpace(bank.Ispb))
                continue;

            var ispb = bank.Ispb.Trim();
            if (ispb.Length > 8)
                continue;

            ispb = ispb.PadLeft(8, '0');
            if (banks.ContainsKey(ispb))
                continue;

            var fullName = FirstNonEmpty(bank.FullName, bank.Name, ispb);
            var shortName = FirstNonEmpty(bank.Name, bank.FullName, ispb);
            var logoUrl = string.IsNullOrWhiteSpace(bank.LogoUrl) ? null : bank.LogoUrl.Trim();
            banks.Add(ispb, new RemoteBank(ispb, bank.Code, fullName, shortName, logoUrl));
        }

        return banks.Values.ToList();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private sealed class BrasilApiBank
    {
        public string? Ispb { get; set; }

        public string? Name { get; set; }

        public int? Code { get; set; }

        public string? FullName { get; set; }

        [JsonPropertyName("logo_url")]
        public string? LogoUrl { get; set; }
    }
}
