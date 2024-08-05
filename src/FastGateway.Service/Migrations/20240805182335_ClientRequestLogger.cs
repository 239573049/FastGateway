using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FastGateway.Service.Migrations
{
    /// <inheritdoc />
    public partial class ClientRequestLogger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_request_logger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<int>(type: "INTEGER", nullable: false),
                    Fail = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestTime = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_request_logger", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_request_logger");
        }
    }
}
