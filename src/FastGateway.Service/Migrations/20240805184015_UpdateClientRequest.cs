using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FastGateway.Service.Migrations
{
    /// <inheritdoc />
    public partial class UpdateClientRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "client_request_logger",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "client_request_logger",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "client_request_logger");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "client_request_logger");
        }
    }
}
