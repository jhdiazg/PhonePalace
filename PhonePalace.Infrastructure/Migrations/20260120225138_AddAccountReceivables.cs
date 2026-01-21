using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountReceivables",
                columns: table => new
                {
                    AccountReceivableID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientID = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SaleID = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountReceivables", x => x.AccountReceivableID);
                    table.ForeignKey(
                        name: "FK_AccountReceivables_Clients_ClientID",
                        column: x => x.ClientID,
                        principalTable: "Clients",
                        principalColumn: "ClientID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountReceivables_Sales_SaleID",
                        column: x => x.SaleID,
                        principalTable: "Sales",
                        principalColumn: "SaleID");
                });

            migrationBuilder.CreateTable(
                name: "AccountReceivablePayments",
                columns: table => new
                {
                    AccountReceivablePaymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountReceivableID = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountReceivablePayments", x => x.AccountReceivablePaymentID);
                    table.ForeignKey(
                        name: "FK_AccountReceivablePayments_AccountReceivables_AccountReceivableID",
                        column: x => x.AccountReceivableID,
                        principalTable: "AccountReceivables",
                        principalColumn: "AccountReceivableID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountReceivablePayments_AccountReceivableID",
                table: "AccountReceivablePayments",
                column: "AccountReceivableID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountReceivables_ClientID",
                table: "AccountReceivables",
                column: "ClientID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountReceivables_SaleID",
                table: "AccountReceivables",
                column: "SaleID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountReceivablePayments");

            migrationBuilder.DropTable(
                name: "AccountReceivables");
        }
    }
}
