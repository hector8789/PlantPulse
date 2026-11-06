using Microsoft.EntityFrameworkCore;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Usuarios;
using PlantPulse.Infrastructure.Security;

namespace PlantPulse.Infrastructure.Persistence.Seed;

/// <summary>
/// Siembra los datos demo. Se engancha con UseSeeding/UseAsyncSeeding (EF Core 9+), así que corre en
/// <c>dotnet ef database update</c>, en <c>Database.Migrate()</c> y en <c>EnsureCreated()</c>.
/// Cada paso es idempotente, así una base sembrada en un incremento anterior recibe solo lo que le falta:
/// <list type="number">
/// <item>Plantas, líneas, causas, órdenes e historial (si no hay plantas).</item>
/// <item>Usuarios demo (si no hay usuarios). Día 2.</item>
/// <item>Costo estándar de las órdenes demo que no lo tengan. Día 3.</item>
/// </list>
/// </summary>
public static class PlantPulseSeeder
{
    public static void Sembrar(DbContext context, DateTimeOffset ahoraUtc) =>
        SembrarAsync(context, ahoraUtc, CancellationToken.None).GetAwaiter().GetResult();

    public static async Task SembrarAsync(DbContext context, DateTimeOffset ahoraUtc, CancellationToken ct)
    {
        if (!await context.Set<Planta>().AnyAsync(ct))
        {
            var datos = DatosDemo.Generar(ahoraUtc);
            context.AddRange(datos.Plantas);          // incluye sus turnos
            context.AddRange(datos.Causas);
            context.AddRange(datos.Lineas);
            context.AddRange(datos.Ordenes);
            context.AddRange(datos.Turnos);           // incluye registros de producción y paros
            await context.SaveChangesAsync(ct);
        }

        if (!await context.Set<Usuario>().AnyAsync(ct))
        {
            var plantas = await context.Set<Planta>().ToDictionaryAsync(p => p.Codigo, p => p.Id, ct);
            var hasher = new Pbkdf2PasswordHasher();
            foreach (var (email, nombre, rol, codigos) in DatosDemo.UsuariosDemo)
            {
                var ids = codigos.Where(plantas.ContainsKey).Select(c => plantas[c]).ToList();
                context.Add(Usuario.Crear(email, nombre, hasher.Hash(DatosDemo.PasswordDemo), [rol], ids));
            }
            await context.SaveChangesAsync(ct);
        }

        var sinCosto = await context.Set<OrdenProduccion>().Where(o => o.CostoUnitarioMxn == null).ToListAsync(ct);
        var asignados = 0;
        foreach (var orden in sinCosto)
        {
            if (DatosDemo.CostoEstandar(orden.MaterialCodigo) is { } costo)
            {
                orden.AsignarCostoEstandar(costo);
                asignados++;
            }
        }
        if (asignados > 0) await context.SaveChangesAsync(ct);
    }
}
