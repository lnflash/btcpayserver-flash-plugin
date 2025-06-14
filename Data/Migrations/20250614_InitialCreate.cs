using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BTCPayServer.Plugins.Flash.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlashPayouts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StoreId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    PullPaymentId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    AmountSats = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BoltcardId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    PaymentHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    LightningInvoice = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Memo = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    DestinationAddress = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashPayouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlashPayouts_StoreId_CreatedAt",
                table: "FlashPayouts",
                columns: new[] { "StoreId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashPayouts_Status",
                table: "FlashPayouts",
                column: "Status",
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_FlashPayouts_BoltcardId",
                table: "FlashPayouts",
                column: "BoltcardId",
                filter: "[BoltcardId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FlashPayouts_PaymentHash",
                table: "FlashPayouts",
                column: "PaymentHash");

            migrationBuilder.CreateIndex(
                name: "IX_FlashPayouts_PullPaymentId",
                table: "FlashPayouts",
                column: "PullPaymentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlashPayouts");
        }
    }
}