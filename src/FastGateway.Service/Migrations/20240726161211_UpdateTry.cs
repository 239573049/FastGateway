using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FastGateway.Service.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TryFiles",
                table: "domain_name",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "setting",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Group = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_setting", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_setting_IsPublic",
                table: "setting",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_setting_IsSystem",
                table: "setting",
                column: "IsSystem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "setting");

            migrationBuilder.DropColumn(
                name: "TryFiles",
                table: "domain_name");
        }
    }
}
