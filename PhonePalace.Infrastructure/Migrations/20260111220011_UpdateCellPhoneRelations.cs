using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhonePalace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCellPhoneRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Asignar un ModelID por defecto a los CellPhones que no lo tienen
            migrationBuilder.Sql("UPDATE Products SET ModelID = (SELECT TOP 1 ModelID FROM Models WHERE IsActive = 1) WHERE ProductType = 'CellPhone' AND ModelID IS NULL;");

            // Asignar un CategoryID por defecto a los CellPhones que no lo tienen
            migrationBuilder.Sql("UPDATE Products SET CategoryID = (SELECT TOP 1 CategoryID FROM Categories WHERE IsActive = 1) WHERE ProductType = 'CellPhone' AND CategoryID IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
