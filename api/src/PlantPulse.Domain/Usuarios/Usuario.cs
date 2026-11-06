using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.Usuarios;

/// <summary>Roles del sistema. Se guardan como texto y viajan en el claim "role" del JWT.</summary>
public static class Roles
{
    /// <summary>Acceso a todas las plantas; único que reabre turnos.</summary>
    public const string Admin = "Admin";

    /// <summary>Captura producción y paros en sus plantas.</summary>
    public const string Supervisor = "Supervisor";

    /// <summary>Solo lectura (catálogos, turnos, KPIs) en sus plantas.</summary>
    public const string Consulta = "Consulta";

    public static readonly IReadOnlyList<string> Todos = [Admin, Supervisor, Consulta];
}

/// <summary>
/// Usuario de PlantPulse. El acceso por planta se define aquí y se replica en el JWT (claim "planta")
/// y, en el Día 4, en la identidad efectiva del RLS de Power BI.
/// </summary>
public sealed class Usuario : AggregateRoot
{
    public static class Errores
    {
        public const string DatoInvalido = "usuario.dato_invalido";
        public const string RolInvalido = "usuario.rol_invalido";
        public const string SinPlantas = "usuario.sin_plantas";
    }

    public string Email { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string[] Roles { get; private set; } = [];

    /// <summary>Plantas permitidas. Un Admin ve todas aunque la lista esté vacía.</summary>
    public Guid[] PlantaIds { get; private set; } = [];

    public bool Activo { get; private set; }

    public bool EsAdmin => Roles.Contains(Usuarios.Roles.Admin);

    private Usuario() { }

    private Usuario(Guid id) : base(id) { }

    public static Usuario Crear(string email, string nombre, string passwordHash, IEnumerable<string> roles, IEnumerable<Guid> plantaIds)
    {
        var correo = Guard.Texto(email, 120, Errores.DatoInvalido, "El email").ToLowerInvariant();
        if (!correo.Contains('@') || correo.StartsWith('@') || correo.EndsWith('@'))
            throw new DomainException(Errores.DatoInvalido, $"El email '{correo}' no es válido.");

        var listaRoles = roles.Select(r => r.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (listaRoles.Length == 0 || listaRoles.Any(r => !Usuarios.Roles.Todos.Contains(r)))
            throw new DomainException(Errores.RolInvalido, $"Roles inválidos: {string.Join(", ", listaRoles)}.");

        var plantas = plantaIds.Where(p => p != Guid.Empty).Distinct().ToArray();
        if (!listaRoles.Contains(Usuarios.Roles.Admin) && plantas.Length == 0)
            throw new DomainException(Errores.SinPlantas, "Un usuario que no es Admin necesita al menos una planta.");

        return new Usuario(NuevoId())
        {
            Email = correo,
            Nombre = Guard.Texto(nombre, 120, Errores.DatoInvalido, "El nombre"),
            PasswordHash = Guard.Texto(passwordHash, 256, Errores.DatoInvalido, "El hash de contraseña"),
            Roles = listaRoles,
            PlantaIds = plantas,
            Activo = true,
        };
    }

    public bool PuedeAccederPlanta(Guid plantaId) => EsAdmin || PlantaIds.Contains(plantaId);

    public void CambiarPasswordHash(string passwordHash) =>
        PasswordHash = Guard.Texto(passwordHash, 256, Errores.DatoInvalido, "El hash de contraseña");

    public void Desactivar() => Activo = false;
}
