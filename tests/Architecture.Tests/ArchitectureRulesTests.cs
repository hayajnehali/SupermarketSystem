using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Domain_projects_must_only_reference_building_blocks()
    {
        AssertProjectReferences(
            "src/Central/Central.Domain/Central.Domain.csproj",
            "src/BuildingBlocks/Supermarket.BuildingBlocks/Supermarket.BuildingBlocks.csproj");
        AssertProjectReferences(
            "src/Branch/Branch.Domain/Branch.Domain.csproj",
            "src/BuildingBlocks/Supermarket.BuildingBlocks/Supermarket.BuildingBlocks.csproj");
    }

    [Fact]
    public void Application_projects_must_depend_only_on_inward_contracts()
    {
        AssertProjectReferences(
            "src/Central/Central.Application/Central.Application.csproj",
            "src/Central/Central.Domain/Central.Domain.csproj",
            "src/BuildingBlocks/Supermarket.BuildingBlocks/Supermarket.BuildingBlocks.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
        AssertProjectReferences(
            "src/Branch/Branch.Application/Branch.Application.csproj",
            "src/Branch/Branch.Domain/Branch.Domain.csproj",
            "src/BuildingBlocks/Supermarket.BuildingBlocks/Supermarket.BuildingBlocks.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
    }

    [Fact]
    public void Infrastructure_projects_may_implement_application_and_domain_contracts()
    {
        AssertProjectReferences(
            "src/Central/Central.Infrastructure/Central.Infrastructure.csproj",
            "src/Central/Central.Application/Central.Application.csproj",
            "src/Central/Central.Domain/Central.Domain.csproj",
            "src/BuildingBlocks/Supermarket.BuildingBlocks/Supermarket.BuildingBlocks.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
        AssertProjectReferences(
            "src/Branch/Branch.Infrastructure/Branch.Infrastructure.csproj",
            "src/Branch/Branch.Application/Branch.Application.csproj",
            "src/Branch/Branch.Domain/Branch.Domain.csproj",
            "src/BuildingBlocks/Supermarket.BuildingBlocks/Supermarket.BuildingBlocks.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
    }

    [Fact]
    public void Hosts_must_be_the_only_outer_composition_layers()
    {
        AssertProjectReferences(
            "src/Central/Central.Api/Central.Api.csproj",
            "src/Central/Central.Application/Central.Application.csproj",
            "src/Central/Central.Infrastructure/Central.Infrastructure.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
        AssertProjectReferences(
            "src/Central/Central.Worker/Central.Worker.csproj",
            "src/Central/Central.Application/Central.Application.csproj",
            "src/Central/Central.Infrastructure/Central.Infrastructure.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
        AssertProjectReferences(
            "src/Branch/Branch.Api/Branch.Api.csproj",
            "src/Branch/Branch.Application/Branch.Application.csproj",
            "src/Branch/Branch.Infrastructure/Branch.Infrastructure.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
        AssertProjectReferences(
            "src/Branch/Branch.Worker/Branch.Worker.csproj",
            "src/Branch/Branch.Application/Branch.Application.csproj",
            "src/Branch/Branch.Infrastructure/Branch.Infrastructure.csproj",
            "src/Contracts/Supermarket.Contracts/Supermarket.Contracts.csproj");
    }

    [Fact]
    public void Domain_and_application_must_not_reference_entity_framework_core()
    {
        var protectedProjects = new[]
        {
            "src/Central/Central.Domain/Central.Domain.csproj",
            "src/Central/Central.Application/Central.Application.csproj",
            "src/Branch/Branch.Domain/Branch.Domain.csproj",
            "src/Branch/Branch.Application/Branch.Application.csproj"
        };

        foreach (var relativePath in protectedProjects)
        {
            var project = XDocument.Load(FullPath(relativePath));
            var packageReferences = project.Descendants()
                .Where(element => element.Name.LocalName == "PackageReference")
                .Select(element => (string?)element.Attribute("Include") ?? string.Empty);

            Assert.DoesNotContain(packageReferences, package =>
                package.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));

            var sourceDirectory = Path.GetDirectoryName(FullPath(relativePath))!;
            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(sourceFile);
                Assert.False(source.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal),
                    $"EF Core namespace found in protected layer: {Path.GetRelativePath(Root, sourceFile)}");
            }
        }
    }

    [Fact]
    public void Repositories_must_never_commit_the_unit_of_work()
    {
        var repositoryDirectories = Directory.EnumerateDirectories(
            FullPath("src"), "Repositories", SearchOption.AllDirectories);

        foreach (var directory in repositoryDirectories)
        foreach (var sourceFile in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(sourceFile);
            Assert.False(Regex.IsMatch(source, @"\bSaveChanges(?:Async)?\s*\("),
                $"Repository commits persistence directly: {Path.GetRelativePath(Root, sourceFile)}");
        }
    }

    [Fact]
    public void Product_unit_must_not_have_a_standalone_repository()
    {
        var forbiddenNames = new[]
        {
            "ProductUnitRepository.cs",
            "IProductUnitRepository.cs"
        };

        var sourceFiles = Directory.EnumerateFiles(FullPath("src"), "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var forbiddenName in forbiddenNames)
            Assert.DoesNotContain(forbiddenName, sourceFiles);
    }

    [Fact]
    public void MediatR_must_not_be_added_without_architecture_approval()
    {
        var projectFiles = Directory.EnumerateFiles(FullPath("src"), "*.csproj", SearchOption.AllDirectories);
        foreach (var projectFile in projectFiles)
        {
            var project = File.ReadAllText(projectFile);
            Assert.False(project.Contains("MediatR", StringComparison.OrdinalIgnoreCase),
                $"MediatR package/reference found in {Path.GetRelativePath(Root, projectFile)}");
        }

        foreach (var sourceFile in Directory.EnumerateFiles(FullPath("src"), "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(sourceFile);
            Assert.False(source.Contains("using MediatR", StringComparison.Ordinal),
                $"MediatR namespace found in {Path.GetRelativePath(Root, sourceFile)}");
        }
    }

    private static void AssertProjectReferences(string projectRelativePath, params string[] expectedReferences)
    {
        var projectPath = FullPath(projectRelativePath);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var project = XDocument.Load(projectPath);

        var actualReferences = project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element =>
            {
                var include = (string?)element.Attribute("Include")
                    ?? throw new InvalidOperationException($"ProjectReference without Include in {projectRelativePath}");
                var absolutePath = Path.GetFullPath(Path.Combine(projectDirectory, include));
                return Path.GetRelativePath(Root, absolutePath).Replace('\\', '/');
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var expected = expectedReferences.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actualReferences);
    }

    private static string FullPath(string relativePath) => Path.Combine(Root, relativePath);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SupermarketSystem.sln")))
                return directory.FullName;

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SupermarketSystem.sln")))
                return directory.FullName;

        throw new DirectoryNotFoundException("Could not locate SupermarketSystem.sln to run architecture tests.");
    }
}
