using System.Reflection;

using Gym.Domain.Common;

namespace Gym.Domain.Tests.Architecture;

/// <summary>
/// Executable form of the dependency rule in docs/ARCHITECTURE.md: the Domain layer is the
/// centre of the onion and depends on nothing. These tests fail the build the moment someone
/// adds a ProjectReference or PackageReference to Gym.Domain.csproj that pulls in another layer.
/// </summary>
public sealed class DomainDependencyTests
{
    private static readonly AssemblyName[] ReferencedAssemblies =
        AssemblyReference.Assembly.GetReferencedAssemblies();

    [Fact]
    public void DomainAssembly_WhenInspected_ReferencesNoOtherSolutionAssembly()
    {
        var solutionReferences = ReferencedAssemblies
            .Where(assembly => assembly.Name!.StartsWith("Gym.", StringComparison.Ordinal))
            .Select(assembly => assembly.Name)
            .ToArray();

        solutionReferences.ShouldBeEmpty(
            "Gym.Domain must not depend on any other layer of the solution.");
    }

    [Fact]
    public void DomainAssembly_WhenInspected_ReferencesNoEntityFrameworkCore()
    {
        var efReferences = ReferencedAssemblies
            .Where(assembly => assembly.Name!.Contains("EntityFrameworkCore", StringComparison.Ordinal))
            .Select(assembly => assembly.Name)
            .ToArray();

        efReferences.ShouldBeEmpty(
            "persistence concerns belong in Gym.Infrastructure, not in the Domain.");
    }

    [Fact]
    public void DomainAssembly_WhenInspected_ReferencesNoAspNetCore()
    {
        var webReferences = ReferencedAssemblies
            .Where(assembly => assembly.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .Select(assembly => assembly.Name)
            .ToArray();

        webReferences.ShouldBeEmpty(
            "the Domain must not know that this application is exposed over HTTP.");
    }
}
