using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorFixedExpensesStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FixedExpenses_CashMovements_CashMovementId",
                table: "FixedExpenses");

            migrationBuilder.DropIndex(
                name: "IX_FixedExpenses_CashMovementId",
                table: "FixedExpenses");

            migrationBuilder.DropColumn(
                name: "CashMovementId",
                table: "FixedExpenses");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "FixedExpenses");

            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "FixedExpenses");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "FixedExpenses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FixedExpenses");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "FixedExpenses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FixedExpensePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixedExpenseId = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Period = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CashMovementId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedExpensePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedExpensePayments_CashMovements_CashMovementId",
                        column: x => x.CashMovementId,
                        principalTable: "CashMovements",
                        principalColumn: "CashMovementID");
                    table.ForeignKey(
                        name: "FK_FixedExpensePayments_FixedExpenses_FixedExpenseId",
                        column: x => x.FixedExpenseId,
                        principalTable: "FixedExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixedExpensePayments_CashMovementId",
                table: "FixedExpensePayments",
                column: "CashMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedExpensePayments_FixedExpenseId",
                table: "FixedExpensePayments",
                column: "FixedExpenseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixedExpensePayments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "FixedExpenses");

            migrationBuilder.AddColumn<int>(
                name: "CashMovementId",
                table: "FixedExpenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "FixedExpenses",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDate",
                table: "FixedExpenses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "FixedExpenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FixedExpenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FixedExpenses_CashMovementId",
                table: "FixedExpenses",
                column: "CashMovementId");

            migrationBuilder.AddForeignKey(
                name: "FK_FixedExpenses_CashMovements_CashMovementId",
                table: "FixedExpenses",
                column: "CashMovementId",
                principalTable: "CashMovements",
                principalColumn: "CashMovementID");
        }
    }
}
