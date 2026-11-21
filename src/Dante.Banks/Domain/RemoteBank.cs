namespace Dante.Banks;

public sealed record RemoteBank(string Ispb, int? Code, string FullName, string ShortName, string? LogoUrl);

public sealed record BankSyncResult(int Inserted, int Updated);
