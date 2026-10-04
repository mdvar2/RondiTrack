using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureRelationshipsAndQueryModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new payout recipient user column separately.
            // The old StokvelMemberId contains the surrogate membership ID,
            // so it must not simply be renamed to RecipientUserId.
            migrationBuilder.AddColumn<Guid>(
                name: "RecipientUserId",
                table: "Payouts",
                type: "uuid",
                nullable: true);

            // Translate the old membership reference into the corresponding
            // UserId while the old StokvelMembers.Id column still exists.
            migrationBuilder.Sql(
                """
                UPDATE "Payouts" AS p
                SET "RecipientUserId" = sm."UserId"
                FROM "StokvelMembers" AS sm
                WHERE p."StokvelMemberId" = sm."Id";
                """);

            // Fail rather than silently inventing or losing payout recipients.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Payouts"
                        WHERE "RecipientUserId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot migrate payouts because one or more StokvelMemberId values do not match an existing membership.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipientUserId",
                table: "Payouts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "StokvelMemberId",
                table: "Payouts");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "StokvelMembers",
                type: "text",
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_StokvelMembers_StokvelId_UserId",
                table: "StokvelMembers");

            // Only remove the old surrogate key after payout references
            // have been translated to UserId.
            migrationBuilder.DropColumn(
                name: "Id",
                table: "StokvelMembers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers",
                columns: new[] { "UserId", "StokvelId" });

            migrationBuilder.CreateIndex(
                name: "IX_StokvelMembers_StokvelId",
                table: "StokvelMembers",
                column: "StokvelId");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_RecipientUserId_StokvelId",
                table: "Payouts",
                columns: new[] { "RecipientUserId", "StokvelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions",
                column: "ContributionCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_UserId_StokvelId",
                table: "Contributions",
                columns: new[] { "UserId", "StokvelId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_ContributionCycles_ContributionCycleId",
                table: "Contributions",
                column: "ContributionCycleId",
                principalTable: "ContributionCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_StokvelMembers_UserId_StokvelId",
                table: "Contributions",
                columns: new[] { "UserId", "StokvelId" },
                principalTable: "StokvelMembers",
                principalColumns: new[] { "UserId", "StokvelId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payouts_StokvelMembers_RecipientUserId_StokvelId",
                table: "Payouts",
                columns: new[] { "RecipientUserId", "StokvelId" },
                principalTable: "StokvelMembers",
                principalColumns: new[] { "UserId", "StokvelId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StokvelMembers_Stokvels_StokvelId",
                table: "StokvelMembers",
                column: "StokvelId",
                principalTable: "Stokvels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_ContributionCycles_ContributionCycleId",
                table: "Contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_StokvelMembers_UserId_StokvelId",
                table: "Contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_Payouts_StokvelMembers_RecipientUserId_StokvelId",
                table: "Payouts");

            migrationBuilder.DropForeignKey(
                name: "FK_StokvelMembers_Stokvels_StokvelId",
                table: "StokvelMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_StokvelMembers_StokvelId",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_RecipientUserId_StokvelId",
                table: "Payouts");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_UserId_StokvelId",
                table: "Contributions");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "StokvelMembers",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "StokvelMemberId",
                table: "Payouts",
                type: "uuid",
                nullable: true);

            // Reconstruct the old payout membership reference using the
            // newly generated surrogate membership IDs.
            migrationBuilder.Sql(
                """
                UPDATE "Payouts" AS p
                SET "StokvelMemberId" = sm."Id"
                FROM "StokvelMembers" AS sm
                WHERE p."RecipientUserId" = sm."UserId"
                  AND p."StokvelId" = sm."StokvelId";
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Payouts"
                        WHERE "StokvelMemberId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot roll back payouts because a matching membership could not be reconstructed.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "StokvelMemberId",
                table: "Payouts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "StokvelMembers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StokvelMembers",
                table: "StokvelMembers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StokvelMembers_StokvelId_UserId",
                table: "StokvelMembers",
                columns: new[] { "StokvelId", "UserId" },
                unique: true);
        }
    }
}