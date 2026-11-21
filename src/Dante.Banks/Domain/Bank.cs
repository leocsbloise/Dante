namespace Dante.Banks;

public sealed class Bank
{
    public Guid Id { get; set; }

    public required string Ispb { get; set; }

    public int? Code { get; set; }

    public required string FullName { get; set; }

    public required string ShortName { get; set; }

    public string? LogoUrl { get; set; }

    public static Bank Create(RemoteBank remote)
    {
        if (remote.Ispb.Length != 8)
            throw new InvalidBankException("ISPB must have 8 characters.");

        if (string.IsNullOrWhiteSpace(remote.FullName) || string.IsNullOrWhiteSpace(remote.ShortName))
            throw new InvalidBankException("Bank name is required.");

        return new Bank
        {
            Id = Guid.NewGuid(),
            Ispb = remote.Ispb,
            Code = remote.Code,
            FullName = remote.FullName,
            ShortName = remote.ShortName,
            LogoUrl = remote.LogoUrl,
        };
    }

    public bool HasSameCatalogData(RemoteBank remote)
    {
        return Code == remote.Code
            && FullName == remote.FullName
            && ShortName == remote.ShortName
            && LogoUrl == remote.LogoUrl;
    }

    public void ApplyCatalog(RemoteBank remote)
    {
        if (remote.Ispb != Ispb)
            throw new InvalidBankException("Catalog entry does not belong to this bank.");

        if (string.IsNullOrWhiteSpace(remote.FullName) || string.IsNullOrWhiteSpace(remote.ShortName))
            throw new InvalidBankException("Bank name is required.");

        Code = remote.Code;
        FullName = remote.FullName;
        ShortName = remote.ShortName;
        LogoUrl = remote.LogoUrl;
    }
}
