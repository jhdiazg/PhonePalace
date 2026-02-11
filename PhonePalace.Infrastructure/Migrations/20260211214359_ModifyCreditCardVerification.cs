using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModifyCreditCardVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditCardVerifications_Payments_PaymentID",
                table: "CreditCardVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditCardVerifications_Sales_SaleID",
                table: "CreditCardVerifications");

            migrationBuilder.AlterColumn<int>(
                name: "SaleID",
                table: "CreditCardVerifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentID",
                table: "CreditCardVerifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "AccountReceivablePaymentID",
                table: "CreditCardVerifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardVerifications_AccountReceivablePaymentID",
                table: "CreditCardVerifications",
                column: "AccountReceivablePaymentID");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCardVerifications_AccountReceivablePayments_AccountReceivablePaymentID",
                table: "CreditCardVerifications",
                column: "AccountReceivablePaymentID",
                principalTable: "AccountReceivablePayments",
                principalColumn: "AccountReceivablePaymentID");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCardVerifications_Payments_PaymentID",
                table: "CreditCardVerifications",
                column: "PaymentID",
                principalTable: "Payments",
                principalColumn: "PaymentID");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCardVerifications_Sales_SaleID",
                table: "CreditCardVerifications",
                column: "SaleID",
                principalTable: "Sales",
                principalColumn: "SaleID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditCardVerifications_AccountReceivablePayments_AccountReceivablePaymentID",
                table: "CreditCardVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditCardVerifications_Payments_PaymentID",
                table: "CreditCardVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditCardVerifications_Sales_SaleID",
                table: "CreditCardVerifications");

            migrationBuilder.DropIndex(
                name: "IX_CreditCardVerifications_AccountReceivablePaymentID",
                table: "CreditCardVerifications");

            migrationBuilder.DropColumn(
                name: "AccountReceivablePaymentID",
                table: "CreditCardVerifications");

            migrationBuilder.AlterColumn<int>(
                name: "SaleID",
                table: "CreditCardVerifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PaymentID",
                table: "CreditCardVerifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCardVerifications_Payments_PaymentID",
                table: "CreditCardVerifications",
                column: "PaymentID",
                principalTable: "Payments",
                principalColumn: "PaymentID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCardVerifications_Sales_SaleID",
                table: "CreditCardVerifications",
                column: "SaleID",
                principalTable: "Sales",
                principalColumn: "SaleID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
