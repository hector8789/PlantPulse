using FluentValidation;
using MediatR;
using PlantPulse.Application.Common;

namespace PlantPulse.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public sealed record UsuarioSesionDto(Guid Id, string Nombre, string Email, IReadOnlyList<string> Roles, IReadOnlyList<Guid> PlantaIds);

/// <summary>Forma acordada con el frontend: { accessToken, expiresAtUtc, user: { id, nombre, roles, plantaIds } }.</summary>
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UsuarioSesionDto User);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no es válido.")
            .MaximumLength(120);
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(200);
    }
}

public sealed class LoginCommandHandler(
    IUsuarioRepository usuarios,
    IPlantaRepository plantas,
    IPasswordHasher hasher,
    IJwtTokenGenerator tokens) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ObtenerPorEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);

        // Mismo mensaje para usuario inexistente, inactivo o contraseña incorrecta: no revela qué falló.
        if (usuario is null || !usuario.Activo || !hasher.Verificar(request.Password, usuario.PasswordHash))
            throw new UnauthorizedException("Email o contraseña incorrectos.");

        // Un Admin recibe todas las plantas para que el frontend pueda armar sus selectores.
        IReadOnlyList<Guid> plantaIds = usuario.EsAdmin
            ? (await plantas.ListarAsync(null, cancellationToken)).Select(p => p.Id).ToList()
            : usuario.PlantaIds;

        var token = tokens.Generar(usuario, [.. plantaIds]);
        return new LoginResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new UsuarioSesionDto(usuario.Id, usuario.Nombre, usuario.Email, usuario.Roles, plantaIds));
    }
}
