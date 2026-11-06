using System.Reflection;

namespace PlantPulse.Domain;

/// <summary>Marcador del ensamblado de dominio. El dominio no depende de ningún paquete externo.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
