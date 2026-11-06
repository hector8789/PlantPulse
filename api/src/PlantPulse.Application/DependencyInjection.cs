using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PlantPulse.Application.Common;
using PlantPulse.Application.Kpis;
using PlantPulse.Application.Turnos;

namespace PlantPulse.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        services.AddScoped<TurnoLineaDtoFactory>();
        services.AddScoped<TiposCambioService>();
        return services;
    }
}
