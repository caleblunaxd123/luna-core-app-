using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LunaCore.Api.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Planes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrecioMensual = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    LimiteMensajes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsosMensuales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NegocioId = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Mensajes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsosMensuales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Negocios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rubro = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Negocios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Negocios_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Planes",
                columns: new[] { "Id", "LimiteMensajes", "Nombre", "PrecioMensual" },
                values: new object[,]
                {
                    { 1, 50, "Free", 0m },
                    { 2, 1000, "Starter", 59m },
                    { 3, 5000, "Growth", 149m },
                    { 4, 20000, "Pro", 299m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Negocios_Email",
                table: "Negocios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Negocios_PlanId",
                table: "Negocios",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UsosMensuales_NegocioId_Periodo",
                table: "UsosMensuales",
                columns: new[] { "NegocioId", "Periodo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Negocios");

            migrationBuilder.DropTable(
                name: "UsosMensuales");

            migrationBuilder.DropTable(
                name: "Planes");
        }
    }
}
