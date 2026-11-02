using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assistant_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QdrantBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RestartRequired = table.Column<bool>(type: "boolean", nullable: false),
                    LastIndexSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_settings");
        }
    }
}
