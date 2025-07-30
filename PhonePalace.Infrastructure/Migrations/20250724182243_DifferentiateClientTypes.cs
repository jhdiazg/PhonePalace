using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DifferentiateClientTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NIT",
                table: "Clients",
                newName: "NIT");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "ClientType",
                table: "Clients",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Clients",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "Clients",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "Clients",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "NIT",
                table: "Clients",
                newName: "NIT");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Clients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
