using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAbsenceApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_employee_absences_EmployeeId",
                table: "employee_absences");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "employee_absences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedBy",
                table: "employee_absences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedAt",
                table: "employee_absences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "RequestedBy",
                table: "employee_absences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "employee_absences",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_absences_EmployeeId_Status",
                table: "employee_absences",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RecipientEmployeeId",
                table: "notifications",
                column: "RecipientEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_employee_absences_EmployeeId_Status",
                table: "employee_absences");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "employee_absences");

            migrationBuilder.DropColumn(
                name: "DecidedBy",
                table: "employee_absences");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "employee_absences");

            migrationBuilder.DropColumn(
                name: "RequestedBy",
                table: "employee_absences");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "employee_absences");

            migrationBuilder.CreateIndex(
                name: "IX_employee_absences_EmployeeId",
                table: "employee_absences",
                column: "EmployeeId");
        }
    }
}
