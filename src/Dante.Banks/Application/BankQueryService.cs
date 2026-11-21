namespace Dante.Banks;

public interface IBankQueryService
{
    Task<IReadOnlyList<BankResponse>> ListAsync(CancellationToken cancellationToken);
}

public sealed class BankQueryService(IBankRepository banks) : IBankQueryService
{
    public async Task<IReadOnlyList<BankResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var banksInCatalog = await banks.ListAsync(cancellationToken);
        return banksInCatalog
            .OrderBy(bank => bank.ShortName, StringComparer.Ordinal)
            .Select(BankResponse.From)
            .ToList();
    }
}
