using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankID",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Banks",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BankTransactions",
                columns: table => new
                {
                    BankTransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankID = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BalanceAfterTransaction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactions", x => x.BankTransactionID);
                    table.ForeignKey(
                        name: "FK_BankTransactions_Banks_BankID",
                        column: x => x.BankID,
                        principalTable: "Banks",
                        principalColumn: "BankID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankTransactions_Payments_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "Payments",
                        principalColumn: "PaymentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BankID",
                table: "Payments",
                column: "BankID");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_BankID",
                table: "BankTransactions",
                column: "BankID");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_PaymentID",
                table: "BankTransactions",
                column: "PaymentID");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Banks_BankID",
                table: "Payments",
                column: "BankID",
                principalTable: "Banks",
                principalColumn: "BankID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Banks_BankID",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_BankID",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BankID",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Banks");
        }
    }
}
