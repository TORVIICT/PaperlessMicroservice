using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ocr_jobs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_storage_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processing_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_text = table.Column<string>(type: "text", nullable: true),
                    result_page_count = table.Column<int>(type: "integer", nullable: true),
                    result_duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    result_engine_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    result_archived_pdf_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    result_thumbnail_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ocr_jobs", x => x.job_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ocr_jobs");
        }
    }
}
