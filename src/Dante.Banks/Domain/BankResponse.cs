namespace Dante.Banks;

public sealed record BankResponse(Guid Id, string Ispb, int? Code, string FullName, string ShortName, string? LogoUrl)
{
    public static BankResponse From(Bank bank) =>
        new(bank.Id, bank.Ispb, bank.Code, bank.FullName, bank.ShortName, bank.LogoUrl);
}
