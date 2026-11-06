namespace PlantPulse.Domain.Common;

/// <summary>Validaciones de entrada reutilizables. Lanzan <see cref="DomainException"/>.</summary>
internal static class Guard
{
    public static string Texto(string? valor, int maximo, string codigo, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException(codigo, $"{campo} es obligatorio.");
        var limpio = valor.Trim();
        if (limpio.Length > maximo)
            throw new DomainException(codigo, $"{campo} no puede exceder {maximo} caracteres.");
        return limpio;
    }

    public static string? TextoOpcional(string? valor, int maximo, string codigo, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim();
        if (limpio.Length > maximo)
            throw new DomainException(codigo, $"{campo} no puede exceder {maximo} caracteres.");
        return limpio;
    }

    /// <summary>PostgreSQL guarda microsegundos; truncar a segundos evita diferencias al releer.</summary>
    public static DateTimeOffset Utc(DateTimeOffset valor)
    {
        var ticks = valor.UtcTicks - (valor.UtcTicks % TimeSpan.TicksPerSecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
