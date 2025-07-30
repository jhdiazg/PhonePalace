using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSchemaAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Brands",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Brands",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Brands");
        }
    }
}
