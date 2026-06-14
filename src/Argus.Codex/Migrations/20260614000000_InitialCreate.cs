using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Codex.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Hosts",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                MachineName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                OperatingSystem = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                AgentVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ApiKeyHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Hosts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Hosts_ApiKeyHash",
            table: "Hosts",
            column: "ApiKeyHash");

        migrationBuilder.CreateIndex(
            name: "IX_Hosts_MachineName",
            table: "Hosts",
            column: "MachineName");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Hosts");
    }
}
