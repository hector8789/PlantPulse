using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PlantPulse.Application.Common;
using PlantPulse.Domain.Usuarios;

namespace PlantPulse.Api.Security;

/// <summary>Genera el access token HS256 con sub, name, email, role[] y planta[].</summary>
public sealed class JwtTokenGenerator(IOptions<JwtSettings> options, TimeProvider reloj) : IJwtTokenGenerator
{
    private static readonly JsonWebTokenHandler Handler = new();

    public TokenGenerado Generar(Usuario usuario, IReadOnlyCollection<Guid> plantaIds)
    {
        var s = options.Value;
        var ahora = reloj.GetUtcNow();
        var expira = ahora.AddMinutes(s.ExpiracionMinutos);

        var claims = new List<Claim>
        {
            new(ClaimsPlantPulse.Sub, usuario.Id.ToString()),
            new(ClaimsPlantPulse.Nombre, usuario.Nombre),
            new(ClaimsPlantPulse.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(usuario.Roles.Select(r => new Claim(ClaimsPlantPulse.Rol, r)));
        claims.AddRange(plantaIds.Select(p => new Claim(ClaimsPlantPulse.Planta, p.ToString())));

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = s.Issuer,
            Audience = s.Audience,
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = new SigningCredentials(s.CrearLlave(), SecurityAlgorithms.HmacSha256),
        });

        return new TokenGenerado(token, expira);
    }
}

/// <summary>Usuario actual a partir de los claims del JWT.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(ClaimsPlantPulse.Sub)?.Value, out var id) ? id : null;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimsPlantPulse.Rol).Select(c => c.Value).ToArray() ?? [];

    public IReadOnlyCollection<Guid> PlantaIds =>
        Principal?.FindAll(ClaimsPlantPulse.Planta)
            .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToArray() ?? [];
}
