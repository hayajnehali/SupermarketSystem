using Central.Application.Abstractions;
using Central.Application.ProductCatalog;
using Central.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Central.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCentralInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CentralDatabase")
            ?? throw new InvalidOperationException("Connection string 'CentralDatabase' is required.");

        services.AddDbContext<CentralDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IProductRepository>(provider => provider.GetRequiredService<CentralDbContext>());
        services.AddScoped<ICentralUnitOfWork>(provider => provider.GetRequiredService<CentralDbContext>());

        return services;
    }
}
