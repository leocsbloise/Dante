using System.Runtime.CompilerServices;

namespace Dante.Server.Tests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Dante",
            "Host=localhost;Port=5432;Database=dante;Username=dante;Password=dante");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey",
            "0123456789abcdef0123456789abcdef");
        Environment.SetEnvironmentVariable("Banks__SyncOnStartup", "false");
        Environment.SetEnvironmentVariable("Database__ApplyMigrations", "false");
    }
}
