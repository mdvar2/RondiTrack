using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseDefenseConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payouts_StokvelId_ContributionCycleId",
                table: "Payouts",
                columns: new[] { "StokvelId", "ContributionCycleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payouts_StokvelId_ContributionCycleId",
                table: "Payouts");
        }
    }
}
