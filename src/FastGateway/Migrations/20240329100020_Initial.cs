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
                name: "location",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceId = table.Column<string>(type: "TEXT", nullable: false),
                    ProxyPass = table.Column<string>(type: "TEXT", nullable: true),
                    AddHeader = table.Column<string>(type: "TEXT", nullable: false),
                    Root = table.Column<string>(type: "TEXT", nullable: true),
                    TryFiles = table.Column<string>(type: "TEXT", nullable: true),
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

            migrationBuilder.CreateIndex(
                name: "IX_location_ServiceId",
                table: "location",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location");

            migrationBuilder.DropTable(
                name: "service");
        }
    }
}
