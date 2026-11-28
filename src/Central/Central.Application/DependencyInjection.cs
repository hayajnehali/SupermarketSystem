using Central.Application.ProductCatalog;
using Microsoft.Extensions.DependencyInjection;

namespace Central.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCentralApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateProductHandler>();
        return services;
    }
}
