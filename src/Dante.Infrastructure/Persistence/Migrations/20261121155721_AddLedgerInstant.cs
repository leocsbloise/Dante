using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dante.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerInstant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OccurredUtcTicks",
                table: "LedgerEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE "LedgerEntries"
                SET "OccurredUtcTicks" = (EXTRACT(EPOCH FROM "OccurredAt") * 10000000)::bigint + 621355968000000000
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OccurredUtcTicks",
                table: "LedgerEntries");
        }
    }
}
