using Central.Application.Abstractions;
using Central.Application.ProductCatalog.Interfaces;
using Central.Infrastructure.Execution;
using Central.Infrastructure.Persistence;
using Central.Infrastructure.ProductCatalog.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.BuildingBlocks.Identity;
using Supermarket.BuildingBlocks.Operations;
using Supermarket.BuildingBlocks.Time;

namespace Central.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCentralInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CentralDatabase")
            ?? throw new InvalidOperationException("Connection string 'CentralDatabase' is required.");

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();
        services.AddScoped<IOperationContext, HttpOperationContext>();

        services.AddDbContext<CentralDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICentralUnitOfWork>(provider => provider.GetRequiredService<CentralDbContext>());

        return services;
    }
}
