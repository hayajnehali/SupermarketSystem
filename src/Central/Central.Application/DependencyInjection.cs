using Central.Application.ProductCatalog.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Central.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCentralApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        return services;
    }
}
