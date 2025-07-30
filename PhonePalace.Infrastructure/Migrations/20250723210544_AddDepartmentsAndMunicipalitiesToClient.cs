using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentsAndMunicipalitiesToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Clients",
                newName: "StreetAddress");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentID",
                table: "Clients",
                type: "nvarchar(2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipalityID",
                table: "Clients",
                type: "nvarchar(5)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    DepartmentID = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.DepartmentID);
                });

            migrationBuilder.CreateTable(
                name: "Municipalities",
                columns: table => new
                {
                    MunicipalityID = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepartmentID = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Municipalities", x => x.MunicipalityID);
                    table.ForeignKey(
                        name: "FK_Municipalities_Departments_DepartmentID",
                        column: x => x.DepartmentID,
                        principalTable: "Departments",
                        principalColumn: "DepartmentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_DepartmentID",
                table: "Clients",
                column: "DepartmentID");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_MunicipalityID",
                table: "Clients",
                column: "MunicipalityID");

            migrationBuilder.CreateIndex(
                name: "IX_Municipalities_DepartmentID",
                table: "Municipalities",
                column: "DepartmentID");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Departments_DepartmentID",
                table: "Clients",
                column: "DepartmentID",
                principalTable: "Departments",
                principalColumn: "DepartmentID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Municipalities_MunicipalityID",
                table: "Clients",
                column: "MunicipalityID",
                principalTable: "Municipalities",
                principalColumn: "MunicipalityID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Departments_DepartmentID",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Municipalities_MunicipalityID",
                table: "Clients");

            migrationBuilder.DropTable(
                name: "Municipalities");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Clients_DepartmentID",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_MunicipalityID",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "DepartmentID",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "MunicipalityID",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "StreetAddress",
                table: "Clients",
                newName: "Address");
        }
    }
}
