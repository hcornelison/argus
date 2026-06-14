using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Codex.Migrations;

/// <inheritdoc />
public partial class RemoveApiKeyHash : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Hosts_ApiKeyHash",
            table: "Hosts");

        migrationBuilder.DropIndex(
            name: "IX_Hosts_MachineName",
            table: "Hosts");

        migrationBuilder.DropColumn(
            name: "ApiKeyHash",
            table: "Hosts");

        migrationBuilder.CreateIndex(
            name: "IX_Hosts_MachineName",
            table: "Hosts",
            column: "MachineName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Hosts_MachineName",
            table: "Hosts");

        migrationBuilder.AddColumn<string>(
            name: "ApiKeyHash",
            table: "Hosts",
            type: "TEXT",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_Hosts_ApiKeyHash",
            table: "Hosts",
            column: "ApiKeyHash");

        migrationBuilder.CreateIndex(
            name: "IX_Hosts_MachineName",
            table: "Hosts",
            column: "MachineName");
    }
}
