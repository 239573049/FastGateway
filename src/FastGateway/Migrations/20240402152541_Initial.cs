using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FastGateway.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cert",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Domains = table.Column<string>(type: "TEXT", nullable: false),
                    Expired = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoRenew = table.Column<bool>(type: "INTEGER", nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    RenewTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RenewStats = table.Column<int>(type: "INTEGER", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Certs = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cert", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "service",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceNames = table.Column<string>(type: "TEXT", nullable: false),
                    Listen = table.Column<ushort>(type: "INTEGER", nullable: false),
                    EnableHttp3 = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsHttps = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableFlowMonitoring = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableRequestSource = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableTunnel = table.Column<bool>(type: "INTEGER", nullable: false),
                    SslCertificate = table.Column<string>(type: "TEXT", nullable: true),
                    SslCertificatePassword = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "statistic_ip",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ip = table.Column<string>(type: "TEXT", nullable: true),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<ushort>(type: "INTEGER", nullable: false),
                    Month = table.Column<byte>(type: "INTEGER", nullable: false),
                    Day = table.Column<byte>(type: "INTEGER", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statistic_ip", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "location",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyPass = table.Column<string>(type: "TEXT", nullable: true),
                    AddHeader = table.Column<string>(type: "TEXT", nullable: false),
                    Root = table.Column<string>(type: "TEXT", nullable: true),
                    TryFiles = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    LoadType = table.Column<int>(type: "INTEGER", nullable: false),
                    UpStreams = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location", x => x.Id);
                    table.ForeignKey(
                        name: "FK_location_service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "service",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "statistic_request_count",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: false),
                    RequestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Error4xxCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Error5xxCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<ushort>(type: "INTEGER", nullable: false),
                    Month = table.Column<byte>(type: "INTEGER", nullable: false),
                    Day = table.Column<byte>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statistic_request_count", x => x.Id);
                    table.ForeignKey(
                        name: "FK_statistic_request_count_service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "service",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_location_ServiceId",
                table: "location",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_statistic_request_count_ServiceId",
                table: "statistic_request_count",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cert");

            migrationBuilder.DropTable(
                name: "location");

            migrationBuilder.DropTable(
                name: "statistic_ip");

            migrationBuilder.DropTable(
                name: "statistic_request_count");

            migrationBuilder.DropTable(
                name: "service");
        }
    }
}
