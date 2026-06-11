using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LunaCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class AgenteConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentesConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NegocioId = table.Column<int>(type: "int", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NombreAgente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseConocimiento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Horarios = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Adelanto = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentesConfig", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentesConfig_NegocioId",
                table: "AgentesConfig",
                column: "NegocioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentesConfig");
        }
    }
}
