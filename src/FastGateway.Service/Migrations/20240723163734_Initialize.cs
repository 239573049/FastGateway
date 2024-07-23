using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FastGateway.Service.Migrations
{
    /// <inheritdoc />
    public partial class Initialize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blacklist_and_whitelist",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ips = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsBlacklist = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blacklist_and_whitelist", x => x.Id);
                });

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
                    RenewStats = table.Column<byte>(type: "INTEGER", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Certs = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cert", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "domain_name",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    ServerId = table.Column<string>(type: "TEXT", nullable: false),
                    Domains = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Headers = table.Column<string>(type: "TEXT", nullable: false),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Service = table.Column<string>(type: "TEXT", nullable: true),
                    UpStreams = table.Column<string>(type: "TEXT", nullable: false),
                    Root = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_name", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rate_limit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: false),
                    Period = table.Column<string>(type: "TEXT", nullable: false),
                    Limit = table.Column<int>(type: "INTEGER", nullable: false),
                    EndpointWhitelist = table.Column<string>(type: "TEXT", nullable: false),
                    IpWhitelist = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_limit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "server",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Listen = table.Column<ushort>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    RedirectHttps = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    StaticCompress = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsHttps = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableTunnel = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableBlacklist = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableWhitelist = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blacklist_and_whitelist_Enable",
                table: "blacklist_and_whitelist",
                column: "Enable");

            migrationBuilder.CreateIndex(
                name: "IX_blacklist_and_whitelist_Ips",
                table: "blacklist_and_whitelist",
                column: "Ips");

            migrationBuilder.CreateIndex(
                name: "IX_blacklist_and_whitelist_IsBlacklist",
                table: "blacklist_and_whitelist",
                column: "IsBlacklist");

            migrationBuilder.CreateIndex(
                name: "IX_blacklist_and_whitelist_Name",
                table: "blacklist_and_whitelist",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_domain_name_Enable",
                table: "domain_name",
                column: "Enable");

            migrationBuilder.CreateIndex(
                name: "IX_domain_name_Root",
                table: "domain_name",
                column: "Root");

            migrationBuilder.CreateIndex(
                name: "IX_domain_name_ServerId",
                table: "domain_name",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_domain_name_Service",
                table: "domain_name",
                column: "Service");

            migrationBuilder.CreateIndex(
                name: "IX_domain_name_ServiceType",
                table: "domain_name",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_rate_limit_Enable",
                table: "rate_limit",
                column: "Enable");

            migrationBuilder.CreateIndex(
                name: "IX_server_Enable",
                table: "server",
                column: "Enable");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blacklist_and_whitelist");

            migrationBuilder.DropTable(
                name: "cert");

            migrationBuilder.DropTable(
                name: "domain_name");

            migrationBuilder.DropTable(
                name: "rate_limit");

            migrationBuilder.DropTable(
                name: "server");
        }
    }
}
