using Central.Domain.ProductCatalog;
using Central.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Supermarket.BuildingBlocks.Identity;
using Supermarket.BuildingBlocks.Time;
using Xunit;

namespace Central.SqlServer.IntegrationTests.ProductCatalog;

public sealed class ProductSkuSqlServerTests
{
    [Fact]
    public async Task Sku_Should_Preserve_Original_Casing_And_Enforce_Case_Insensitive_Uniqueness()
    {
        var connectionString = Environment.GetEnvironmentVariable("SUPERMARKET_SQLSERVER_TEST_CONNECTION");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "SUPERMARKET_SQLSERVER_TEST_CONNECTION must point to an isolated SQL Server test database.");

        var actor = new TestCurrentActor(Guid.NewGuid());
        var clock = new TestClock();
        var options = new DbContextOptionsBuilder<CentralDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        var connected = false;
        for (var attempt = 0; attempt < 45; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(masterConnectionString);
                await connection.OpenAsync();
                connected = true;
                break;
            }
            catch (SqlException) when (attempt < 44)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        Assert.True(connected, "SQL Server did not become available within 90 seconds.");

        await using var db = new CentralDbContext(options, actor, clock);

        // This is a disposable, isolated CI database. No production migration is created or applied.
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var categoryId = Guid.NewGuid();
        db.ProductCategories.Add(ProductCategory.Create(
            categoryId,
            parentCategoryId: null,
            categoryCode: "SKU-TEST",
            name: "SKU Integration Test",
            description: null,
            displayOrder: 0));

        var productId = Guid.NewGuid();
        db.Products.Add(Product.Create(
            productId,
            sku: "MiXeD-Sku-01",
            name: "SKU casing test",
            categoryId: categoryId));

        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var storedSku = await db.Products
            .AsNoTracking()
            .Where(x => x.Id == productId)
            .Select(x => x.Sku)
            .SingleAsync();

        Assert.Equal("MiXeD-Sku-01", storedSku);

        db.Products.Add(Product.Create(
            Guid.NewGuid(),
            sku: "mixed-sku-01",
            name: "Duplicate SKU test",
            categoryId: categoryId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private sealed class TestCurrentActor(Guid userId) : ICurrentActor
    {
        public Guid? UserId { get; } = userId;
        public Guid? EmployeeId => null;
        public Guid? BranchId => null;
        public string? DeviceId => "sql-server-integration-test";
        public bool IsAuthenticated => true;
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
