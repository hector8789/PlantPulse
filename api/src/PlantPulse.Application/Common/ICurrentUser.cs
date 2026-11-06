using PlantPulse.Domain.Usuarios;

namespace PlantPulse.Application.Common;

/// <summary>Usuario de la petición actual. La API lo implementa leyendo los claims del JWT.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<Guid> PlantaIds { get; }
}

public static class CurrentUserExtensions
{
    public static bool EsAdmin(this ICurrentUser user) => user.Roles.Contains(Domain.Usuarios.Roles.Admin);

    public static bool PuedeAccederPlanta(this ICurrentUser user, Guid plantaId) =>
        user.EsAdmin() || user.PlantaIds.Contains(plantaId);

    /// <summary>Lanza 403 si la planta no es del usuario.</summary>
    public static void AsegurarAccesoPlanta(this ICurrentUser user, Guid plantaId)
    {
        if (!user.PuedeAccederPlanta(plantaId))
            throw new ForbiddenAccessException("No tienes acceso a esa planta.");
    }

    /// <summary>
    /// Plantas a consultar: la pedida (validando acceso) o todas las del usuario.
    /// Devuelve null cuando no hay que filtrar (Admin sin planta específica).
    /// </summary>
    public static IReadOnlyCollection<Guid>? PlantasVisibles(this ICurrentUser user, Guid? plantaId)
    {
        if (plantaId is { } id)
        {
            user.AsegurarAccesoPlanta(id);
            return [id];
        }
        return user.EsAdmin() ? null : user.PlantaIds;
    }
}
