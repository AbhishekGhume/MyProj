using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XmlMiddleware.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessingBatches",
                columns: table => new
                {
                    BatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentStep = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ReceivedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingEndDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LeaseOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeaseExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingBatches", x => x.BatchId);
                });

            migrationBuilder.CreateTable(
                name: "OutputFiles",
                columns: table => new
                {
                    OutputFileId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    OutputType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    OutputBlobPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutputFiles", x => x.OutputFileId);
                    table.ForeignKey(
                        name: "FK_OutputFiles_ProcessingBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ProcessingBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingEvents",
                columns: table => new
                {
                    EventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    OutputFileId = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FunctionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingEvents", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_ProcessingEvents_OutputFiles_OutputFileId",
                        column: x => x.OutputFileId,
                        principalTable: "OutputFiles",
                        principalColumn: "OutputFileId");
                    table.ForeignKey(
                        name: "FK_ProcessingEvents_ProcessingBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ProcessingBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutputFiles_BatchId_OutputType",
                table: "OutputFiles",
                columns: new[] { "BatchId", "OutputType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingBatches_CorrelationId",
                table: "ProcessingBatches",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingBatches_FileHash",
                table: "ProcessingBatches",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingBatches_Status",
                table: "ProcessingBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingEvents_BatchId",
                table: "ProcessingEvents",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingEvents_CorrelationId",
                table: "ProcessingEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingEvents_EventType",
                table: "ProcessingEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingEvents_OutputFileId",
                table: "ProcessingEvents",
                column: "OutputFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessingEvents");

            migrationBuilder.DropTable(
                name: "OutputFiles");

            migrationBuilder.DropTable(
                name: "ProcessingBatches");
        }
    }
}
