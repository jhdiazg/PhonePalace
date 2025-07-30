using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                table: "Models",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Models",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedOn",
                table: "Models");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Models");
        }
    }
}
