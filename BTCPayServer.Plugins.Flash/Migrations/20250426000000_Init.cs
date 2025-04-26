using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BTCPayServer.Plugins.Flash.Data;

namespace BTCPayServer.Plugins.Flash.Migrations
{
    [DbContext(typeof(FlashCardDbContext))]
    [Migration("20250426000000_Init")]
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.Flash");

            migrationBuilder.CreateTable(
                name: "CardRegistrations",
                schema: "BTCPayServer.Plugins.Flash",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    CardUID = table.Column<string>(nullable: false),
                    PullPaymentId = table.Column<string>(nullable: false),
                    StoreId = table.Column<string>(nullable: false),
                    UserId = table.Column<string>(nullable: true),
                    CardName = table.Column<string>(nullable: false),
                    Version = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(nullable: true),
                    IsBlocked = table.Column<bool>(nullable: false),
                    FlashWalletId = table.Column<string>(nullable: true),
                    SpendingLimitPerTransaction = table.Column<decimal>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardTransactions",
                schema: "BTCPayServer.Plugins.Flash",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    CardRegistrationId = table.Column<string>(nullable: false),
                    PayoutId = table.Column<string>(nullable: true),
                    Amount = table.Column<decimal>(nullable: false),
                    Currency = table.Column<string>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    InvoiceId = table.Column<string>(nullable: true),
                    PaymentHash = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(nullable: true),
                    MerchantId = table.Column<string>(nullable: true),
                    LocationId = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardTransactions_CardRegistrations_CardRegistrationId",
                        column: x => x.CardRegistrationId,
                        principalSchema: "BTCPayServer.Plugins.Flash",
                        principalTable: "CardRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardRegistrations_CardUID",
                schema: "BTCPayServer.Plugins.Flash",
                table: "CardRegistrations",
                column: "CardUID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardRegistrations_PullPaymentId",
                schema: "BTCPayServer.Plugins.Flash",
                table: "CardRegistrations",
                column: "PullPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CardTransactions_CardRegistrationId",
                schema: "BTCPayServer.Plugins.Flash",
                table: "CardTransactions",
                column: "CardRegistrationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardTransactions",
                schema: "BTCPayServer.Plugins.Flash");

            migrationBuilder.DropTable(
                name: "CardRegistrations",
                schema: "BTCPayServer.Plugins.Flash");
        }
    }
}