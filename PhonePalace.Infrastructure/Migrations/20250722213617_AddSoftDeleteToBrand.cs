using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToBrand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_Products_ProductID",
                table: "Inventory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Inventory",
                table: "Inventory");

            migrationBuilder.RenameTable(
                name: "Inventory",
                newName: "Inventories");

            migrationBuilder.RenameIndex(
                name: "IX_Inventory_ProductID",
                table: "Inventories",
                newName: "IX_Inventories_ProductID");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Categories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Inventories",
                table: "Inventories",
                column: "InventoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventories_Products_ProductID",
                table: "Inventories",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventories_Products_ProductID",
                table: "Inventories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Inventories",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Categories");

            migrationBuilder.RenameTable(
                name: "Inventories",
                newName: "Inventory");

            migrationBuilder.RenameIndex(
                name: "IX_Inventories_ProductID",
                table: "Inventory",
                newName: "IX_Inventory_ProductID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Inventory",
                table: "Inventory",
                column: "InventoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Products_ProductID",
                table: "Inventory",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
