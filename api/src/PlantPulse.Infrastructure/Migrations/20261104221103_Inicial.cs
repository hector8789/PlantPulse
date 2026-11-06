using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "causas_paro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    categoria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_causas_paro", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plantas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    zona_horaria = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lineas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    planta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tiempo_ciclo_estandar_seg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lineas", x => x.id);
                    table.ForeignKey(
                        name: "fk_lineas_plantas_planta_id",
                        column: x => x.planta_id,
                        principalTable: "plantas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "turnos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    planta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turnos", x => x.id);
                    table.ForeignKey(
                        name: "fk_turnos_plantas_planta_id",
                        column: x => x.planta_id,
                        principalTable: "plantas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_produccion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    planta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    material_descripcion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cantidad_planeada = table.Column<int>(type: "integer", nullable: false),
                    fecha_inicio_plan = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin_plan = table.Column<DateOnly>(type: "date", nullable: false),
                    tiempo_ciclo_ideal_seg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordenes_produccion", x => x.id);
                    table.ForeignKey(
                        name: "fk_ordenes_produccion_lineas_linea_id",
                        column: x => x.linea_id,
                        principalTable: "lineas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ordenes_produccion_plantas_planta_id",
                        column: x => x.planta_id,
                        principalTable: "plantas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "turnos_linea",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    planta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_operativa = table.Column<DateOnly>(type: "date", nullable: false),
                    inicio_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fin_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    abierto_en_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cerrado_en_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turnos_linea", x => x.id);
                    table.ForeignKey(
                        name: "fk_turnos_linea_lineas_linea_id",
                        column: x => x.linea_id,
                        principalTable: "lineas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_turnos_linea_plantas_planta_id",
                        column: x => x.planta_id,
                        principalTable: "plantas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_turnos_linea_turnos_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turnos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registros_paro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_linea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    causa_paro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    inicio_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fin_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    comentario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    registrado_en_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registros_paro", x => x.id);
                    table.CheckConstraint("ck_registros_paro_rango", "fin_utc > inicio_utc");
                    table.ForeignKey(
                        name: "fk_registros_paro_causas_paro_causa_paro_id",
                        column: x => x.causa_paro_id,
                        principalTable: "causas_paro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registros_paro_turnos_linea_turno_linea_id",
                        column: x => x.turno_linea_id,
                        principalTable: "turnos_linea",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registros_produccion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_linea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_produccion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tiempo_ciclo_ideal_seg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    piezas_totales = table.Column<int>(type: "integer", nullable: false),
                    piezas_buenas = table.Column<int>(type: "integer", nullable: false),
                    registrado_en_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modificado_en_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registros_produccion", x => x.id);
                    table.CheckConstraint("ck_registros_produccion_piezas", "piezas_buenas >= 0 AND piezas_buenas <= piezas_totales");
                    table.ForeignKey(
                        name: "fk_registros_produccion_ordenes_produccion_orden_produccion_id",
                        column: x => x.orden_produccion_id,
                        principalTable: "ordenes_produccion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registros_produccion_turnos_linea_turno_linea_id",
                        column: x => x.turno_linea_id,
                        principalTable: "turnos_linea",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_causas_paro_codigo",
                table: "causas_paro",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lineas_planta_id_codigo",
                table: "lineas",
                columns: new[] { "planta_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_produccion_linea_id_fecha_inicio_plan",
                table: "ordenes_produccion",
                columns: new[] { "linea_id", "fecha_inicio_plan" });

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_produccion_numero",
                table: "ordenes_produccion",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_produccion_planta_id",
                table: "ordenes_produccion",
                column: "planta_id");

            migrationBuilder.CreateIndex(
                name: "ix_plantas_codigo",
                table: "plantas",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_registros_paro_causa_paro_id",
                table: "registros_paro",
                column: "causa_paro_id");

            migrationBuilder.CreateIndex(
                name: "ix_registros_paro_turno_linea_id_inicio_utc",
                table: "registros_paro",
                columns: new[] { "turno_linea_id", "inicio_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_registros_produccion_orden_produccion_id",
                table: "registros_produccion",
                column: "orden_produccion_id");

            migrationBuilder.CreateIndex(
                name: "ix_registros_produccion_turno_linea_id_orden_produccion_id",
                table: "registros_produccion",
                columns: new[] { "turno_linea_id", "orden_produccion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turnos_planta_id_numero",
                table: "turnos",
                columns: new[] { "planta_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turnos_linea_linea_id_fecha_operativa_turno_id",
                table: "turnos_linea",
                columns: new[] { "linea_id", "fecha_operativa", "turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turnos_linea_planta_id_fecha_operativa",
                table: "turnos_linea",
                columns: new[] { "planta_id", "fecha_operativa" });

            migrationBuilder.CreateIndex(
                name: "ix_turnos_linea_turno_id",
                table: "turnos_linea",
                column: "turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros_paro");

            migrationBuilder.DropTable(
                name: "registros_produccion");

            migrationBuilder.DropTable(
                name: "causas_paro");

            migrationBuilder.DropTable(
                name: "ordenes_produccion");

            migrationBuilder.DropTable(
                name: "turnos_linea");

            migrationBuilder.DropTable(
                name: "lineas");

            migrationBuilder.DropTable(
                name: "turnos");

            migrationBuilder.DropTable(
                name: "plantas");
        }
    }
}
