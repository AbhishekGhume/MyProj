using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XmlMiddleware.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "ProcessingBatches",
                newName: "StoredFileName");

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "ProcessingBatches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "ProcessingBatches");

            migrationBuilder.RenameColumn(
                name: "StoredFileName",
                table: "ProcessingBatches",
                newName: "FileName");
        }
    }
}
