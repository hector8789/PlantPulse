using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlantPulse.Application.Auth;
using PlantPulse.Application.Common;

namespace PlantPulse.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Inicia sesión y devuelve el JWT con roles y plantas.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginCommand command, CancellationToken ct) =>
        Ok(await sender.Send(command, ct));

    /// <summary>Lo que el API entiende del token actual (útil para depurar roles y plantas).</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me([FromServices] ICurrentUser usuario) =>
        Ok(new { id = usuario.UserId, roles = usuario.Roles, plantaIds = usuario.PlantaIds });
}
