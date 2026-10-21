using Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, string? autoMapperLicenseKey = null)
    {
        //>>> AutoMapper
        //AutoMapper 15+ is dual licensed (Community edition is free, register a key at https://automapper.io).
        //Optional key is read from appsettings: "AutoMapper": { "LicenseKey": "..." }
        services.AddAutoMapper(cfg =>
        {
            if (!string.IsNullOrWhiteSpace(autoMapperLicenseKey))
            {
                cfg.LicenseKey = autoMapperLicenseKey;
            }
        }, Assembly.GetExecutingAssembly());

        //>>> FluentValidation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        //>>> MediatR
        services.AddMediatR(x =>
        {
            x.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            x.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            x.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        });

        //>>> Register services in Application.Features 
        var assembly = Assembly.GetExecutingAssembly();
        //>>> Records are plain data holders (DTOs), never services. Compiler-generated types are skipped too.
        static bool IsRecordType(Type t) => t.GetMethod("<Clone>$") != null;

        //>>> a class whose constructors all need plain values (string, int, ...) cannot be built by the container
        static bool IsContainerBuildable(Type t) =>
            t.GetConstructors().Any(c => c.GetParameters().All(p => !(p.ParameterType.IsPrimitive || p.ParameterType.IsValueType || p.ParameterType == typeof(string))));

        var featureTypes = assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => IsContainerBuildable(type))
            .Where(type => !IsRecordType(type) && !Attribute.IsDefined(type, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)))
            .Where(type => type.Namespace != null && type.Namespace.StartsWith("Application.Features"));

        foreach (var type in featureTypes)
        {
            //>>> framework interfaces (IEquatable, IDisposable, ...) are never injected
            var interfaces = type.GetInterfaces().Where(i => i.Namespace == null || !i.Namespace.StartsWith("System")).ToArray();
            foreach (var serviceInterface in interfaces)
            {
                services.AddScoped(serviceInterface, type);
            }
            if (!interfaces.Any())
            {
                services.AddScoped(type);
            }
        }

        return services;
    }
}

