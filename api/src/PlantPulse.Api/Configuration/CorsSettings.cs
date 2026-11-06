namespace PlantPulse.Api.Configuration;

/// <summary>Sección "Cors" de appsettings.</summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>Orígenes exactos permitidos, p. ej. "http://localhost:5173".</summary>
    public string[] AllowedOrigins { get; init; } = [];
}
