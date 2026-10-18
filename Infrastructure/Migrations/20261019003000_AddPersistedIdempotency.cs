using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Infrastructure.Migrations;

[DbContext(typeof(RondiTrackDbContext))]
[Migration("20261019003000_AddPersistedIdempotency")]
public sealed class AddPersistedIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PersistedIdempotencyRecords",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ResponseStatus = table.Column<int>(type: "integer", nullable: false),
                ResponseJson = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PersistedIdempotencyRecords", x => x.Key));
        migrationBuilder.CreateIndex(
            name: "IX_PersistedIdempotencyRecords_CreatedAtUtc",
            table: "PersistedIdempotencyRecords",
            column: "CreatedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("PersistedIdempotencyRecords");
}
