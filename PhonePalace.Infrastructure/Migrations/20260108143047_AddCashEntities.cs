using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashRegisters",
                columns: table => new
                {
                    CashRegisterID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpeningDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OpenedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClosedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisters", x => x.CashRegisterID);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    CashMovementID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashRegisterID = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentID = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.CashMovementID);
                    table.ForeignKey(
                        name: "FK_CashMovements_CashRegisters_CashRegisterID",
                        column: x => x.CashRegisterID,
                        principalTable: "CashRegisters",
                        principalColumn: "CashRegisterID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashMovements_Payments_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "Payments",
                        principalColumn: "PaymentID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CashRegisterID",
                table: "CashMovements",
                column: "CashRegisterID");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_PaymentID",
                table: "CashMovements",
                column: "PaymentID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CashRegisters");
        }
    }
}
