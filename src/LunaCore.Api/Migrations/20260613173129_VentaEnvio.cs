using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LunaCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class VentaEnvio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Agencia",
                table: "Ventas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cliente",
                table: "Ventas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dni",
                table: "Ventas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Ventas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Ventas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Agencia",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Cliente",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Dni",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Ventas");
        }
    }
}
