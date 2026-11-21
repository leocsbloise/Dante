using Dante.Accounts;
using Dante.Banks;
using Dante.Infrastructure;
using Dante.Ledger;
using Dante.Users;

EnvFile.Load();

var builder = WebApplication.CreateBuilder(args);
builder.AddDanteOpenTelemetry();
builder.Services.AddUsers();
builder.Services.AddAccounts();
builder.Services.AddBanks();
builder.Services.AddLedger();
builder.Services.AddDanteInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();

await DatabaseInitializer.ApplyMigrationsAsync(app.Services, app.Configuration, app.Logger);
app.Run();

public partial class Program;
