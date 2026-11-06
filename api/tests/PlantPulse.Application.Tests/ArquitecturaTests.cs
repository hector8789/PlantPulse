using Xunit;

namespace PlantPulse.Application.Tests;

/// <summary>Protege la regla de dependencias de Clean Architecture.</summary>
public class ArquitecturaTests
{
    private static readonly string[] Prohibidas =
        ["PlantPulse.Infrastructure", "PlantPulse.Api", "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore"];

    [Fact]
    public void Domain_solo_depende_de_la_BCL()
    {
        var referencias = Domain.AssemblyReference.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.All(referencias, nombre =>
            Assert.True(nombre.StartsWith("System", StringComparison.Ordinal) || nombre == "netstandard",
                $"El dominio no debe depender de '{nombre}'."));
    }

    [Fact]
    public void Application_no_depende_de_Infrastructure_ni_de_EF_ni_de_AspNetCore()
    {
        var referencias = AssemblyReference.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.All(referencias, nombre =>
            Assert.DoesNotContain(Prohibidas, p => nombre.StartsWith(p, StringComparison.Ordinal)));
    }
}
