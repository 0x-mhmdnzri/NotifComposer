using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeService.Migrations;

[Migration("20240826000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "employees",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                EmploymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                preferences = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_employees", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_employees_UserId",
            table: "employees",
            column: "UserId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "employees");
    }
}
