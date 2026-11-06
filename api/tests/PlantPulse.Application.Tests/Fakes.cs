using Microsoft.Extensions.Logging.Abstractions;
using PlantPulse.Application.Common;
using PlantPulse.Application.Kpis;
using PlantPulse.Application.Turnos;
using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.TiposCambio;
using PlantPulse.Domain.Usuarios;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Application.Tests;

/// <summary>Fakes en memoria (sin librerías de mocks) para probar handlers.</summary>
internal sealed class Mundo
{
    public static readonly DateOnly Lunes = new(2026, 11, 2);

    /// <summary>Lunes 2026-11-02 20:10 UTC: el Matutino de León (12:00–20:00 UTC) ya terminó.</summary>
    public static readonly DateTimeOffset Ahora = new(2026, 11, 2, 20, 10, 0, TimeSpan.Zero);

    public Planta Leon { get; }
    public Planta Silao { get; }
    public Linea LineaLeon { get; }
    public Linea LineaSilao { get; }
    public OrdenProduccion OrdenLeon { get; }
    public CausaParo Comida { get; }
    public CausaParo FallaMecanica { get; }

    public FakePlantas Plantas { get; }
    public FakeLineas Lineas { get; }
    public FakeCausas Causas { get; }
    public FakeOrdenes Ordenes { get; } = new();
    public FakeTurnos Turnos { get; } = new();
    public FakeTiposCambio TiposCambio { get; } = new();
    public FakeUnitOfWork Uow { get; } = new();
    public RelojFijo Reloj { get; } = new(Ahora);

    public Mundo()
    {
        Leon = Planta.Crear("LEO", "Planta León", "America/Mexico_City");
        Leon.AgregarTurno(1, "Matutino", HorarioTurno.Crear(new(6, 0), new(14, 0)));
        Leon.AgregarTurno(2, "Vespertino", HorarioTurno.Crear(new(14, 0), new(22, 0)));
        Silao = Planta.Crear("SIL", "Planta Silao", "America/Mexico_City");
        Silao.AgregarTurno(1, "Matutino", HorarioTurno.Crear(new(7, 0), new(15, 0)));

        LineaLeon = Linea.Crear(Leon, "LEO-L01", "Fundición 1", TiempoCiclo.DeSegundos(45m));
        LineaSilao = Linea.Crear(Silao, "SIL-L01", "Arneses", TiempoCiclo.DeSegundos(30m));
        OrdenLeon = OrdenProduccion.Crear("1000001", LineaLeon, "CAR-4521", "Carcasa", 1500, Lunes, Lunes.AddDays(2), null, 400m);
        Ordenes.Lista.Add(OrdenLeon);

        Comida = CausaParo.Crear("PLN-COM", "Comida", TipoParo.Planificado, CategoriaParo.Programado);
        FallaMecanica = CausaParo.Crear("NP-MEC", "Falla mecánica", TipoParo.NoPlanificado, CategoriaParo.Mecanica);

        Plantas = new FakePlantas([Leon, Silao]);
        Lineas = new FakeLineas([LineaLeon, LineaSilao]);
        Causas = new FakeCausas([Comida, FallaMecanica]);
    }

    public Turno Matutino => Leon.Turnos.Single(t => t.Numero == 1);

    public TurnoLineaDtoFactory Dtos => new(Plantas, Lineas, Causas, Ordenes);

    public FakeUsuario Supervisor(params Planta[] plantas) => new([Roles.Supervisor], [.. plantas.Select(p => p.Id)]);
    public static FakeUsuario Admin() => new([Roles.Admin], []);

    public TurnoLinea TurnoMatutinoAbierto()
    {
        var t = TurnoLinea.Abrir(Leon, LineaLeon, Matutino.Id, Lunes, Ahora);
        Turnos.Lista.Add(t);
        return t;
    }

    public TiposCambioService ServicioTiposCambio(FakeBanxico banxico) =>
        new(TiposCambio, banxico, Uow, Reloj, NullLogger<TiposCambioService>.Instance);
}

internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;
    public override DateTimeOffset GetUtcNow() => Ahora;
}

internal sealed class FakeUsuario(string[] roles, Guid[] plantas) : ICurrentUser
{
    public Guid? UserId { get; } = Guid.CreateVersion7();
    public IReadOnlyCollection<string> Roles { get; } = roles;
    public IReadOnlyCollection<Guid> PlantaIds { get; } = plantas;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Guardados { get; private set; }
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Guardados++;
        return Task.CompletedTask;
    }
}

internal sealed class FakePlantas(List<Planta> lista) : IPlantaRepository
{
    public Task<Planta?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(lista.FirstOrDefault(p => p.Id == id));
    public Task<IReadOnlyList<Planta>> ListarAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Planta>>(lista.Where(p => ids is null || ids.Contains(p.Id)).ToList());
}

internal sealed class FakeLineas(List<Linea> lista) : ILineaRepository
{
    public Task<Linea?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(lista.FirstOrDefault(l => l.Id == id));
    public Task<IReadOnlyList<Linea>> ListarAsync(IReadOnlyCollection<Guid>? ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Linea>>(lista.Where(l => ids is null || ids.Contains(l.PlantaId)).ToList());
}

internal sealed class FakeCausas(List<CausaParo> lista) : ICausaParoRepository
{
    public Task<CausaParo?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(lista.FirstOrDefault(c => c.Id == id));
    public Task<IReadOnlyList<CausaParo>> ListarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CausaParo>>(lista);
}

internal sealed class FakeOrdenes : IOrdenProduccionRepository
{
    public List<OrdenProduccion> Lista { get; } = [];
    public Task<OrdenProduccion?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(Lista.FirstOrDefault(o => o.Id == id));
    public Task<IReadOnlyList<OrdenProduccion>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrdenProduccion>>(Lista.Where(o => ids.Contains(o.Id)).ToList());
    public Task<IReadOnlyList<OrdenProduccion>> ListarPorNumerosAsync(IReadOnlyCollection<string> numeros, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrdenProduccion>>(Lista.Where(o => numeros.Contains(o.Numero)).ToList());
    public Task<IReadOnlyList<OrdenProduccion>> ListarPorLineaAsync(Guid lineaId, bool soloAbiertas, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrdenProduccion>>(Lista.Where(o => o.LineaId == lineaId && (!soloAbiertas || o.AceptaProduccion)).ToList());
    public void Agregar(OrdenProduccion orden) => Lista.Add(orden);
}

internal sealed class FakeTurnos : ITurnoLineaRepository
{
    public List<TurnoLinea> Lista { get; } = [];
    public Task<TurnoLinea?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(Lista.FirstOrDefault(t => t.Id == id));
    public Task<bool> ExisteAsync(Guid lineaId, DateOnly fecha, Guid turnoId, CancellationToken ct) =>
        Task.FromResult(Lista.Any(t => t.LineaId == lineaId && t.FechaOperativa == fecha && t.TurnoId == turnoId));
    public Task<IReadOnlyList<TurnoLinea>> ListarAsync(IReadOnlyCollection<Guid>? plantaIds, Guid? lineaId, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TurnoLinea>>(Lista
            .Where(t => (plantaIds is null || plantaIds.Contains(t.PlantaId)) && (lineaId is null || t.LineaId == lineaId)
                        && t.FechaOperativa >= desde && t.FechaOperativa <= hasta)
            .ToList());
    public void Agregar(TurnoLinea turno) => Lista.Add(turno);
}

internal sealed class FakeTiposCambio : ITipoCambioRepository
{
    public List<TipoCambio> Lista { get; } = [];
    public Task<IReadOnlyList<TipoCambio>> ListarAsync(string serie, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TipoCambio>>(Lista.Where(t => t.Serie == serie && t.Fecha >= desde && t.Fecha <= hasta).OrderBy(t => t.Fecha).ToList());
    public void Agregar(TipoCambio tipoCambio) => Lista.Add(tipoCambio);
}

internal sealed class FakeBanxico(params TipoCambioPublicado[] datos) : ITipoCambioProvider
{
    public bool Falla { get; set; }
    public int Llamadas { get; private set; }

    public Task<IReadOnlyList<TipoCambioPublicado>> ObtenerFixAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        Llamadas++;
        if (Falla) throw new ExternalServiceException("Banxico", "Simulado: Banxico caído.");
        return Task.FromResult<IReadOnlyList<TipoCambioPublicado>>(datos.Where(d => d.Fecha >= desde && d.Fecha <= hasta).ToList());
    }
}

internal sealed class FakeErp(params OrdenErp[] ordenes) : IErpOrdenesClient
{
    public Task<IReadOnlyList<OrdenErp>> ObtenerOrdenesAsync(string plantaCodigo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OrdenErp>>(ordenes);
}

internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;
    public bool Verificar(string password, string hash) => hash == "hash:" + password;
}

internal sealed class FakeTokens : IJwtTokenGenerator
{
    public IReadOnlyCollection<Guid>? UltimasPlantas { get; private set; }
    public TokenGenerado Generar(Usuario usuario, IReadOnlyCollection<Guid> plantaIds)
    {
        UltimasPlantas = plantaIds;
        return new TokenGenerado("token-" + usuario.Email, Mundo.Ahora.AddHours(8));
    }
}

internal sealed class FakeUsuarios(params Usuario[] usuarios) : IUsuarioRepository
{
    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct) =>
        Task.FromResult(usuarios.FirstOrDefault(u => u.Email == email));
}
