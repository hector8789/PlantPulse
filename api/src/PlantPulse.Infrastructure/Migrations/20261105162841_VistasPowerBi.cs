using Microsoft.EntityFrameworkCore.Migrations;
using Vistas = PlantPulse.Infrastructure.Persistence.PowerBi.VistasPowerBi;

#nullable disable

namespace PlantPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VistasPowerBi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Vistas.Crear);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(Vistas.Eliminar);
    }
}