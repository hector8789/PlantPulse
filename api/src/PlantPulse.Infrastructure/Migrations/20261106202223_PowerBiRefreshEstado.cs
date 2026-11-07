using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PowerBiRefreshEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "powerbi_refresh_estado",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    pendiente = table.Column<bool>(type: "boolean", nullable: false),
                    solicitado_en_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_refresh_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_conteo = table.Column<DateOnly>(type: "date", nullable: true),
                    conteo_del_dia = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_powerbi_refresh_estado", x => x.id);
                    table.CheckConstraint("ck_powerbi_refresh_estado_fila_unica", "id = 1");
                });

            migrationBuilder.InsertData(
                table: "powerbi_refresh_estado",
                columns: new[] { "id", "conteo_del_dia", "fecha_conteo", "pendiente", "solicitado_en_utc", "ultimo_refresh_utc" },
                values: new object[] { 1, 0, null, false, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "powerbi_refresh_estado");
        }
    }
}
