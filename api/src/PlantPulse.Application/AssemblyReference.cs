using System.Reflection;

namespace PlantPulse.Application;

/// <summary>Marcador para escanear el ensamblado (MediatR, FluentValidation, pruebas de arquitectura).</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
