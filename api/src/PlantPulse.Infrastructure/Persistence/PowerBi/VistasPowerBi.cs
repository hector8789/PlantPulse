namespace PlantPulse.Infrastructure.Persistence.PowerBi;

/// <summary>
/// Vistas del esquema <c>bi</c> que consume el dataset de Power BI (modelo estrella).
/// Los hechos traen tiempos y piezas sin dividir: los porcentajes (OEE, disponibilidad...) se calculan
/// en DAX sumando primero, igual que el API, para que agreguen bien por línea, turno o mes.
/// Se aplican desde una migración: <c>migrationBuilder.Sql(VistasPowerBi.Crear)</c>.
/// </summary>
public static class VistasPowerBi
{
    public const string Crear = """
        CREATE SCHEMA IF NOT EXISTS bi;

        -- ───────────── Dimensiones ─────────────

        CREATE OR REPLACE VIEW bi.dim_planta AS
        SELECT p.id AS planta_id, p.codigo, p.nombre, p.zona_horaria
        FROM plantas p;

        CREATE OR REPLACE VIEW bi.dim_linea AS
        SELECT l.id AS linea_id, l.planta_id, l.codigo, l.nombre, l.tiempo_ciclo_estandar_seg
        FROM lineas l;

        -- planta_codigo permite filtrar esta dimensión en el RLS (no se relaciona con dim_planta para evitar ambigüedad).
        CREATE OR REPLACE VIEW bi.dim_turno AS
        SELECT t.id AS turno_id, t.planta_id, p.codigo AS planta_codigo, t.numero, t.nombre,
               to_char(t.hora_inicio, 'HH24:MI') AS hora_inicio, to_char(t.hora_fin, 'HH24:MI') AS hora_fin
        FROM turnos t
        JOIN plantas p ON p.id = t.planta_id;

        CREATE OR REPLACE VIEW bi.dim_causa_paro AS
        SELECT c.id AS causa_paro_id, c.codigo, c.descripcion, c.tipo, c.categoria
        FROM causas_paro c;

        CREATE OR REPLACE VIEW bi.dim_orden AS
        SELECT o.id AS orden_produccion_id, o.numero, o.planta_id, p.codigo AS planta_codigo, o.linea_id,
               o.material_codigo, o.material_descripcion, o.cantidad_planeada, o.fecha_inicio_plan, o.fecha_fin_plan,
               o.costo_unitario_mxn, o.estado
        FROM ordenes_produccion o
        JOIN plantas p ON p.id = o.planta_id;

        -- Calendario continuo: desde el primer turno hasta 30 días después del último plan.
        CREATE OR REPLACE VIEW bi.dim_fecha AS
        WITH limites AS (
            SELECT LEAST((SELECT MIN(fecha_operativa) FROM turnos_linea), (SELECT MIN(fecha_inicio_plan) FROM ordenes_produccion), CURRENT_DATE) AS desde,
                   GREATEST((SELECT MAX(fecha_operativa) FROM turnos_linea), (SELECT MAX(fecha_fin_plan) FROM ordenes_produccion), CURRENT_DATE) + 30 AS hasta
        )
        SELECT d::date AS fecha,
               EXTRACT(YEAR FROM d)::int AS anio,
               EXTRACT(MONTH FROM d)::int AS mes,
               to_char(d, 'YYYY-MM') AS anio_mes,
               EXTRACT(ISOYEAR FROM d)::int * 100 + EXTRACT(WEEK FROM d)::int AS anio_semana,
               EXTRACT(ISODOW FROM d)::int AS dia_semana,
               (ARRAY['Lun','Mar','Mié','Jue','Vie','Sáb','Dom'])[EXTRACT(ISODOW FROM d)::int] AS dia_semana_nombre
        FROM limites, generate_series(limites.desde, limites.hasta, interval '1 day') AS d;

        -- ───────────── Hechos ─────────────

        -- Un renglón por turno de línea con los componentes del OEE.
        CREATE OR REPLACE VIEW bi.fact_turno AS
        WITH paros AS (
            SELECT turno_linea_id,
                   SUM(EXTRACT(EPOCH FROM fin_utc - inicio_utc)) FILTER (WHERE tipo = 'Planificado') / 60.0 AS planificados_min,
                   SUM(EXTRACT(EPOCH FROM fin_utc - inicio_utc)) FILTER (WHERE tipo = 'NoPlanificado') / 60.0 AS no_planificados_min
            FROM registros_paro
            GROUP BY turno_linea_id
        ), produccion AS (
            SELECT turno_linea_id,
                   SUM(tiempo_ciclo_ideal_seg * piezas_totales) AS segundos_ideales,
                   SUM(piezas_totales) AS piezas_totales,
                   SUM(piezas_buenas) AS piezas_buenas
            FROM registros_produccion
            GROUP BY turno_linea_id
        ), base AS (
            SELECT t.id AS turno_linea_id, t.planta_id, t.linea_id, t.turno_id, tu.numero AS turno_numero,
                   t.fecha_operativa, t.estado, t.inicio_utc, t.fin_utc,
                   EXTRACT(EPOCH FROM t.fin_utc - t.inicio_utc) / 60.0 AS duracion_min,
                   COALESCE(p.planificados_min, 0) AS paros_planificados_min,
                   COALESCE(p.no_planificados_min, 0) AS paros_no_planificados_min,
                   COALESCE(r.segundos_ideales, 0) AS segundos_ideales,
                   COALESCE(r.piezas_totales, 0) AS piezas_totales,
                   COALESCE(r.piezas_buenas, 0) AS piezas_buenas
            FROM turnos_linea t
            JOIN turnos tu ON tu.id = t.turno_id
            LEFT JOIN paros p ON p.turno_linea_id = t.id
            LEFT JOIN produccion r ON r.turno_linea_id = t.id
        )
        SELECT b.*,
               b.duracion_min - b.paros_planificados_min AS tiempo_planificado_min,
               b.duracion_min - b.paros_planificados_min - b.paros_no_planificados_min AS tiempo_operativo_min,
               b.segundos_ideales / 60.0 AS tiempo_ideal_min,
               b.piezas_totales - b.piezas_buenas AS scrap
        FROM base b;

        -- Un renglón por paro (Pareto de causas).
        CREATE OR REPLACE VIEW bi.fact_paro AS
        SELECT p.id AS registro_paro_id, p.turno_linea_id, t.planta_id, t.linea_id, t.turno_id, t.fecha_operativa,
               p.causa_paro_id, p.tipo, p.inicio_utc, p.fin_utc,
               EXTRACT(EPOCH FROM p.fin_utc - p.inicio_utc) / 60.0 AS duracion_min,
               p.comentario
        FROM registros_paro p
        JOIN turnos_linea t ON t.id = p.turno_linea_id;

        -- Un renglón por registro de producción, con el costo del scrap en MXN y USD.
        -- USD usa el FIX vigente (el del día o el último publicado antes).
        CREATE OR REPLACE VIEW bi.fact_produccion AS
        SELECT r.id AS registro_produccion_id, r.turno_linea_id, t.planta_id, t.linea_id, t.turno_id, t.fecha_operativa,
               r.orden_produccion_id, r.tiempo_ciclo_ideal_seg, r.piezas_totales, r.piezas_buenas,
               r.piezas_totales - r.piezas_buenas AS scrap,
               o.costo_unitario_mxn,
               (r.piezas_totales - r.piezas_buenas) * o.costo_unitario_mxn AS costo_scrap_mxn,
               tc.pesos_por_dolar AS tipo_cambio_fix,
               (r.piezas_totales - r.piezas_buenas) * o.costo_unitario_mxn / NULLIF(tc.pesos_por_dolar, 0) AS costo_scrap_usd
        FROM registros_produccion r
        JOIN turnos_linea t ON t.id = r.turno_linea_id
        JOIN ordenes_produccion o ON o.id = r.orden_produccion_id
        LEFT JOIN LATERAL (
            SELECT x.pesos_por_dolar
            FROM tipos_cambio x
            WHERE x.serie = 'SF43718' AND x.fecha <= t.fecha_operativa
            ORDER BY x.fecha DESC
            LIMIT 1
        ) tc ON TRUE;

        -- Plan contra real por orden de producción.
        CREATE OR REPLACE VIEW bi.fact_orden_plan AS
        SELECT o.id AS orden_produccion_id, o.planta_id, o.linea_id, o.numero, o.material_codigo, o.estado,
               o.fecha_inicio_plan, o.fecha_fin_plan, o.cantidad_planeada,
               COALESCE(SUM(r.piezas_totales), 0) AS piezas_totales_reales,
               COALESCE(SUM(r.piezas_buenas), 0) AS piezas_buenas_reales,
               COALESCE(SUM(r.piezas_buenas), 0)::numeric / NULLIF(o.cantidad_planeada, 0) AS cumplimiento
        FROM ordenes_produccion o
        LEFT JOIN registros_produccion r ON r.orden_produccion_id = o.id
        GROUP BY o.id;
        """;

    public const string Eliminar = """
        DROP VIEW IF EXISTS bi.fact_orden_plan;
        DROP VIEW IF EXISTS bi.fact_produccion;
        DROP VIEW IF EXISTS bi.fact_paro;
        DROP VIEW IF EXISTS bi.fact_turno;
        DROP VIEW IF EXISTS bi.dim_fecha;
        DROP VIEW IF EXISTS bi.dim_orden;
        DROP VIEW IF EXISTS bi.dim_causa_paro;
        DROP VIEW IF EXISTS bi.dim_turno;
        DROP VIEW IF EXISTS bi.dim_linea;
        DROP VIEW IF EXISTS bi.dim_planta;
        DROP SCHEMA IF EXISTS bi;
        """;
}
