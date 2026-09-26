using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XmlMiddleware.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRertyTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessingBatches_FileHash",
                table: "ProcessingBatches");

            migrationBuilder.AddColumn<int>(
                name: "RecordCount",
                table: "ProcessingBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingBatches_FileHash",
                table: "ProcessingBatches",
                column: "FileHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessingBatches_FileHash",
                table: "ProcessingBatches");

            migrationBuilder.DropColumn(
                name: "RecordCount",
                table: "ProcessingBatches");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingBatches_FileHash",
                table: "ProcessingBatches",
                column: "FileHash");
        }
    }
}
