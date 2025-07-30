using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixProductModelRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inventories_ProductID",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "EAN",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "UPC",
                table: "Products",
                newName: "Material");

            migrationBuilder.RenameColumn(
                name: "MPN",
                table: "Products",
                newName: "Compatibility");

            migrationBuilder.RenameColumn(
                name: "JAN",
                table: "Products",
                newName: "Accessory_Color");

            migrationBuilder.AlterColumn<int>(
                name: "StorageGB",
                table: "Products",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ModelID",
                table: "Products",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "BrandID",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "Products",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RamGB",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandID",
                table: "Products",
                column: "BrandID");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductID",
                table: "Inventories",
                column: "ProductID");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_BrandID",
                table: "Products",
                column: "BrandID",
                principalTable: "Brands",
                principalColumn: "BrandID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_BrandID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_BrandID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Inventories_ProductID",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "BrandID",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RamGB",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "Material",
                table: "Products",
                newName: "UPC");

            migrationBuilder.RenameColumn(
                name: "Compatibility",
                table: "Products",
                newName: "MPN");

            migrationBuilder.RenameColumn(
                name: "Accessory_Color",
                table: "Products",
                newName: "JAN");

            migrationBuilder.AlterColumn<int>(
                name: "StorageGB",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SKU",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "ModelID",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "EAN",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductID",
                table: "Inventories",
                column: "ProductID",
                unique: true);
        }
    }
}
