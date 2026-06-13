using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Codex.Migrations
{
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AgentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiskSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostId = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mount = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    UsedBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiskSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiskSamples_Hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "Hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventLogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostId = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventLogEntries_Hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "Hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostId = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Line = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogEntries_Hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "Hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetricSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostId = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CpuPercent = table.Column<double>(type: "float", nullable: false),
                    MemoryTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    MemoryUsedBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricSamples_Hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "Hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostId = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Pid = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CpuPercent = table.Column<double>(type: "float", nullable: false),
                    MemoryBytes = table.Column<long>(type: "bigint", nullable: false),
                    ThreadCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessSamples_Hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "Hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiskSamples_HostId_TimestampUtc",
                table: "DiskSamples",
                columns: new[] { "HostId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EventLogEntries_HostId_TimestampUtc",
                table: "EventLogEntries",
                columns: new[] { "HostId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EventLogEntries_Level",
                table: "EventLogEntries",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Hosts_ApiKeyHash",
                table: "Hosts",
                column: "ApiKeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_Hosts_MachineName",
                table: "Hosts",
                column: "MachineName");

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_FilePath_TimestampUtc",
                table: "LogEntries",
                columns: new[] { "FilePath", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_HostId_TimestampUtc",
                table: "LogEntries",
                columns: new[] { "HostId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricSamples_HostId_TimestampUtc",
                table: "MetricSamples",
                columns: new[] { "HostId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSamples_HostId_TimestampUtc",
                table: "ProcessSamples",
                columns: new[] { "HostId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiskSamples");

            migrationBuilder.DropTable(
                name: "EventLogEntries");

            migrationBuilder.DropTable(
                name: "LogEntries");

            migrationBuilder.DropTable(
                name: "MetricSamples");

            migrationBuilder.DropTable(
                name: "ProcessSamples");

            migrationBuilder.DropTable(
                name: "Hosts");
        }
    }
}
