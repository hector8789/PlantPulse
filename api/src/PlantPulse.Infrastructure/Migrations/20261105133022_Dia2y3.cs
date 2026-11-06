using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Dia2y3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "costo_unitario_mxn",
                table: "ordenes_produccion",
                type: "numeric(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tipos_cambio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    pesos_por_dolar = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    fuente = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    obtenido_en_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_cambio", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    roles = table.Column<string[]>(type: "text[]", nullable: false),
                    planta_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tipos_cambio_serie_fecha",
                table: "tipos_cambio",
                columns: new[] { "serie", "fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tipos_cambio");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropColumn(
                name: "costo_unitario_mxn",
                table: "ordenes_produccion");
        }
    }
}
