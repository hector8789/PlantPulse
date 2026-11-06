using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.Usuarios;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Infrastructure.Persistence.Seed;

/// <summary>
/// Genera datos ficticios realistas usando los métodos del dominio, así que todo respeta las invariantes.
/// Es determinista: misma semilla y mismo "ahora" producen las mismas cifras. No depende de EF Core.
/// <list type="bullet">
/// <item>2 plantas (León y Silao), 3 turnos cada una con horarios distintos.</item>
/// <item>5 líneas, órdenes de 2 a 4 días por línea, algunas con ciclo propio.</item>
/// <item><see cref="DiasHistoriaPorDefecto"/> días de historia sin domingos, todos los turnos cerrados.</item>
/// <item>Pareto realista: falla mecánica y falta de material dominan; LEO-L02 tiene una semana mala.</item>
/// </list>
/// </summary>
public sealed class DatosDemo
{
    public const int DiasHistoriaPorDefecto = 21;
    public const int SemillaPorDefecto = 20261104;

    public required IReadOnlyList<Planta> Plantas { get; init; }
    public required IReadOnlyList<Linea> Lineas { get; init; }
    public required IReadOnlyList<CausaParo> Causas { get; init; }
    public required IReadOnlyList<OrdenProduccion> Ordenes { get; init; }
    public required IReadOnlyList<TurnoLinea> Turnos { get; init; }

    // ───────────── Definiciones ─────────────

    private sealed record DefTurno(int Numero, string Nombre, int HoraInicio, int HoraFin);
    private sealed record DefMaterial(string Codigo, string Descripcion, decimal? CicloPropio, decimal CostoMxn);
    private sealed record DefLinea(
        string Codigo, string Nombre, decimal Ciclo, double DesempenoBase, bool SemanaMala, DefMaterial[] Materiales);
    private sealed record DefPlanta(string Codigo, string Nombre, string Zona, DefTurno[] Turnos, DefLinea[] Lineas);

    private static readonly DefPlanta[] DefPlantas =
    [
        new("LEO", "Planta León", "America/Mexico_City",
            [new(1, "Matutino", 6, 14), new(2, "Vespertino", 14, 22), new(3, "Nocturno", 22, 6)],
            [
                new("LEO-L01", "Fundición a presión 1", 48m, 0.91, false,
                    [new("CAR-4521", "Carcasa de transmisión", null, 412.50m), new("SOP-1180", "Soporte de motor", 40m, 186.90m)]),
                new("LEO-L02", "Fundición a presión 2", 52m, 0.89, true,
                    [new("TAP-3307", "Tapa de cadena de distribución", null, 238.40m), new("CAR-4521", "Carcasa de transmisión", 48m, 412.50m)]),
                new("LEO-L03", "Maquinado CNC", 75m, 0.92, false,
                    [new("CAR-4521-M", "Carcasa de transmisión maquinada", null, 655.00m), new("BRZ-2210", "Brazo de suspensión", 90m, 524.75m)]),
            ]),
        new("SIL", "Planta Silao (Puerto Interior)", "America/Mexico_City",
            [new(1, "Matutino", 7, 15), new(2, "Vespertino", 15, 23), new(3, "Nocturno", 23, 7)],
            [
                new("SIL-L01", "Ensamble de arneses", 30m, 0.90, false,
                    [new("ARN-7702", "Arnés de tablero", null, 318.20m), new("ARN-7710", "Arnés de puerta", 26m, 142.60m)]),
                new("SIL-L02", "Inyección de plástico", 40m, 0.93, false,
                    [new("CLP-0915", "Clip de moldura", null, 6.35m), new("REJ-5530", "Rejilla de ventilación", 44m, 58.90m)]),
            ]),
    ];

    /// <summary>(código, descripción, tipo, categoría, peso en el Pareto, minutos mín., minutos máx.)</summary>
    private static readonly (string Codigo, string Desc, TipoParo Tipo, CategoriaParo Cat, int Peso, int Min, int Max)[] DefCausas =
    [
        ("PLN-COM", "Comida", TipoParo.Planificado, CategoriaParo.Programado, 0, 30, 30),
        ("PLN-MTO", "Mantenimiento preventivo programado", TipoParo.Planificado, CategoriaParo.Programado, 0, 90, 90),
        ("PLN-JUN", "Junta de arranque de turno", TipoParo.Planificado, CategoriaParo.Programado, 0, 10, 10),
        ("NP-MEC", "Falla mecánica", TipoParo.NoPlanificado, CategoriaParo.Mecanica, 30, 10, 55),
        ("NP-MAT", "Falta de material", TipoParo.NoPlanificado, CategoriaParo.Material, 22, 10, 40),
        ("NP-SET", "Cambio de modelo (setup)", TipoParo.NoPlanificado, CategoriaParo.CambioModelo, 14, 20, 40),
        ("NP-HER", "Cambio de herramental o molde", TipoParo.NoPlanificado, CategoriaParo.Mecanica, 11, 15, 35),
        ("NP-CAL", "Ajuste por calidad", TipoParo.NoPlanificado, CategoriaParo.Calidad, 9, 5, 25),
        ("NP-ELE", "Falla eléctrica", TipoParo.NoPlanificado, CategoriaParo.Electrica, 8, 5, 35),
        ("NP-OPE", "Falta de operador", TipoParo.NoPlanificado, CategoriaParo.Operacion, 6, 5, 20),
    ];

    /// <summary>Costo estándar demo por material (MXN por pieza), o null si el material no es de la demo.</summary>
    public static decimal? CostoEstandar(string materialCodigo) =>
        DefPlantas.SelectMany(p => p.Lineas).SelectMany(l => l.Materiales)
            .FirstOrDefault(m => string.Equals(m.Codigo, materialCodigo, StringComparison.OrdinalIgnoreCase))?.CostoMxn;

    /// <summary>Contraseña de todos los usuarios demo. Solo para el entorno de demostración.</summary>
    public const string PasswordDemo = "PlantPulse2026!";

    /// <summary>(email, nombre, rol, códigos de planta). Un Admin no necesita plantas.</summary>
    public static readonly IReadOnlyList<(string Email, string Nombre, string Rol, string[] Plantas)> UsuariosDemo =
    [
        ("admin@plantpulse.mx", "Administrador PlantPulse", Roles.Admin, []),
        ("supervisor.leon@plantpulse.mx", "Supervisor León", Roles.Supervisor, ["LEO"]),
        ("supervisor.silao@plantpulse.mx", "Supervisor Silao", Roles.Supervisor, ["SIL"]),
        ("gerente@plantpulse.mx", "Gerente de Operaciones", Roles.Consulta, ["LEO", "SIL"]),
    ];

    // ───────────── Generación ─────────────

    public static DatosDemo Generar(
        DateTimeOffset ahoraUtc, int diasHistoria = DiasHistoriaPorDefecto, int semilla = SemillaPorDefecto)
    {
        var rnd = new Random(semilla);
        var causas = DefCausas.ToDictionary(c => c.Codigo, c => CausaParo.Crear(c.Codigo, c.Desc, c.Tipo, c.Cat));
        var plantas = new List<Planta>();
        var lineas = new List<Linea>();
        var ordenes = new List<OrdenProduccion>();
        var turnos = new List<TurnoLinea>();
        var consecutivoOrden = 1_000_400;

        foreach (var defPlanta in DefPlantas)
        {
            var planta = Planta.Crear(defPlanta.Codigo, defPlanta.Nombre, defPlanta.Zona);
            foreach (var t in defPlanta.Turnos)
                planta.AgregarTurno(t.Numero, t.Nombre, HorarioTurno.Crear(new TimeOnly(t.HoraInicio, 0), new TimeOnly(t.HoraFin, 0)));
            plantas.Add(planta);

            var hoy = planta.FechaLocal(ahoraUtc);
            var desde = hoy.AddDays(-diasHistoria);

            foreach (var defLinea in defPlanta.Lineas)
            {
                var linea = Linea.Crear(planta, defLinea.Codigo, defLinea.Nombre, TiempoCiclo.DeSegundos(defLinea.Ciclo));
                lineas.Add(linea);

                var ordenesLinea = CrearOrdenes(linea, defLinea, desde.AddDays(-1), hoy.AddDays(6), rnd, ref consecutivoOrden);
                ordenes.AddRange(ordenesLinea);
                var conProduccion = new HashSet<Guid>();

                for (var fecha = desde; fecha < hoy; fecha = fecha.AddDays(1))
                {
                    if (fecha.DayOfWeek == DayOfWeek.Sunday) continue;   // día de descanso

                    var orden = ordenesLinea.Last(o => o.FechaInicioPlan <= fecha);
                    var anterior = ordenesLinea.LastOrDefault(o => o.FechaFinPlan < orden.FechaInicioPlan);

                    foreach (var turno in planta.Turnos.OrderBy(t => t.Numero))
                    {
                        // El primer turno del día en que arranca una orden termina la anterior: dos órdenes y un setup.
                        var cambioDeOrden = turno.Numero == 1 && orden.FechaInicioPlan == fecha && anterior is not null;
                        var turnoLinea = SimularTurno(
                            planta, linea, turno, fecha, defLinea, ahoraUtc, rnd, causas,
                            cambioDeOrden ? [(anterior!, 0.4), (orden, 0.6)] : [(orden, 1.0)],
                            cambioDeOrden, EsSemanaMala(defLinea, fecha, desde));

                        if (turnoLinea is null) continue;
                        turnos.Add(turnoLinea);
                        foreach (var r in turnoLinea.Registros) conProduccion.Add(r.OrdenProduccionId);
                    }
                }

                // Estados finales: terminadas las que ya vencieron, en proceso las que tienen producción.
                foreach (var o in ordenesLinea)
                {
                    if (conProduccion.Contains(o.Id)) o.Iniciar();
                    if (o.FechaFinPlan < hoy && conProduccion.Contains(o.Id)) o.Terminar();
                }
            }
        }

        foreach (var c in causas.Values) c.ClearDomainEvents();
        foreach (var t in turnos) t.ClearDomainEvents();

        return new DatosDemo
        {
            Plantas = plantas,
            Lineas = lineas,
            Causas = [.. causas.Values],
            Ordenes = ordenes,
            Turnos = turnos,
        };
    }

    private static List<OrdenProduccion> CrearOrdenes(
        Linea linea, DefLinea def, DateOnly desde, DateOnly hasta, Random rnd, ref int consecutivo)
    {
        var lista = new List<OrdenProduccion>();
        var inicio = desde;
        var i = 0;
        while (inicio <= hasta)
        {
            var dias = rnd.Next(2, 5);                         // 2 a 4 días
            var fin = inicio.AddDays(dias - 1);
            var material = def.Materiales[i % def.Materiales.Length];
            var ciclo = material.CicloPropio ?? def.Ciclo;

            // ~400 min operativos por turno al desempeño base, 3 turnos por día, ±8%.
            var piezasTurno = 400m * 60m * (decimal)def.DesempenoBase / ciclo;
            var planeada = (int)(Math.Round(piezasTurno * 3 * dias * (decimal)(0.92 + rnd.NextDouble() * 0.16) / 10m) * 10);

            lista.Add(OrdenProduccion.Crear(
                (consecutivo++).ToString(), linea, material.Codigo, material.Descripcion, planeada, inicio, fin,
                material.CicloPropio is { } propio ? TiempoCiclo.DeSegundos(propio) : null,
                material.CostoMxn));

            inicio = fin.AddDays(1);
            i++;
        }
        return lista;
    }

    private static bool EsSemanaMala(DefLinea def, DateOnly fecha, DateOnly desde) =>
        def.SemanaMala && fecha.DayNumber - desde.DayNumber is >= 7 and < 12;

    private static TurnoLinea? SimularTurno(
        Planta planta, Linea linea, Turno turno, DateOnly fecha, DefLinea def, DateTimeOffset ahoraRealUtc, Random rnd,
        Dictionary<string, CausaParo> causas, (OrdenProduccion Orden, double Fraccion)[] reparto,
        bool cambioDeOrden, bool semanaMala)
    {
        var (_, finLocal) = turno.Horario.VentanaLocal(fecha);
        // Se simula que el supervisor captura y cierra 15 minutos después de terminar el turno.
        var ahora = planta.LocalAUtc(finLocal).AddMinutes(15);
        if (ahora > ahoraRealUtc) return null;    // ese turno todavía no termina en la vida real

        var t = TurnoLinea.Abrir(planta, linea, turno.Id, fecha, ahora);
        var minutosTurno = (int)t.DuracionTurno.TotalMinutos;
        var ocupados = new List<(int Ini, int Fin)>();

        bool Colocar(CausaParo causa, int inicioMin, int duracion)
        {
            if (inicioMin < 0 || inicioMin + duracion > minutosTurno) return false;
            if (ocupados.Any(o => inicioMin < o.Fin && o.Ini < inicioMin + duracion)) return false;
            ocupados.Add((inicioMin, inicioMin + duracion));
            t.RegistrarParo(causa, t.InicioUtc.AddMinutes(inicioMin), t.InicioUtc.AddMinutes(inicioMin + duracion), null, ahora);
            return true;
        }

        void ColocarAlAzar(CausaParo causa, int duracion)
        {
            for (var intento = 0; intento < 15; intento++)
                if (Colocar(causa, rnd.Next(0, minutosTurno - duracion + 1), duracion)) return;
        }

        // Paros planificados.
        Colocar(causas["PLN-JUN"], 0, 10);
        if (fecha.DayOfWeek == DayOfWeek.Saturday && turno.Numero == 1) Colocar(causas["PLN-MTO"], 10, 90);
        Colocar(causas["PLN-COM"], minutosTurno / 2, 30);

        // Cambio de orden: setup justo después de la junta.
        if (cambioDeOrden) Colocar(causas["NP-SET"], 10 + (ocupados.Count > 2 ? 90 : 0), rnd.Next(25, 41));

        // Paros no planificados con distribución tipo Pareto.
        var cantidad = rnd.NextDouble() switch { < 0.15 => 0, < 0.45 => 1, < 0.75 => 2, < 0.92 => 3, _ => 4 };
        if (semanaMala) cantidad += 2;
        if (turno.Numero == 3) cantidad += rnd.NextDouble() < 0.3 ? 1 : 0;   // el nocturno sufre un poco más

        for (var i = 0; i < cantidad; i++)
        {
            var def0 = ElegirCausa(rnd, semanaMala);
            var duracion = rnd.Next(def0.Min, def0.Max + 1);
            if (def0.Codigo == "NP-MEC" && rnd.NextDouble() < 0.06) duracion = rnd.Next(60, 121);   // falla mayor
            ColocarAlAzar(causas[def0.Codigo], duracion);
        }

        // Producción: piezas que caben en el tiempo operativo al desempeño simulado.
        var operativoSeg = (double)t.CalcularOee().TiempoOperativo.TotalSegundos;
        var desempeno = def.DesempenoBase + (rnd.NextDouble() * 0.08 - 0.05) - (turno.Numero == 3 ? 0.03 : 0);
        var calidad = 0.955 + rnd.NextDouble() * 0.04;
        if (t.Paros.Any(p => p.CausaParoId == causas["NP-CAL"].Id)) calidad -= 0.025;
        if (semanaMala) calidad -= 0.015;

        foreach (var (orden, fraccion) in reparto)
        {
            var ciclo = (double)orden.ResolverTiempoCiclo(linea).Segundos;
            var totales = (int)Math.Floor(operativoSeg * fraccion * desempeno / ciclo);
            var buenas = (int)Math.Round(totales * calidad);
            t.RegistrarProduccion(orden, linea, totales, buenas, ahora);
        }

        t.Cerrar(ahora);
        return t;
    }

    private static (string Codigo, TipoParo Tipo, CategoriaParo Cat, int Peso, int Min, int Max) ElegirCausa(Random rnd, bool semanaMala)
    {
        var candidatas = DefCausas.Where(c => c.Peso > 0)
            .Select(c => (c.Codigo, c.Tipo, c.Cat, Peso: semanaMala && c.Codigo == "NP-MEC" ? c.Peso * 3 : c.Peso, c.Min, c.Max))
            .ToList();
        var tiro = rnd.Next(candidatas.Sum(c => c.Peso));
        foreach (var c in candidatas)
        {
            if (tiro < c.Peso) return c;
            tiro -= c.Peso;
        }
        return candidatas[^1];
    }
}
