using System.Reflection;

namespace Gym.Api.IntegrationTests.Architecture;

/// <summary>
/// The outward-facing half of the dependency rule: Application may not reach out to
/// Infrastructure or the Api, and Infrastructure may not reach out to the Api.
/// This project references Gym.Api, so every layer's assembly is loadable here.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly AssemblyName[] ApplicationReferences =
        Gym.Application.Common.AssemblyReference.Assembly.GetReferencedAssemblies();

    private static readonly AssemblyName[] InfrastructureReferences =
        Gym.Infrastructure.AssemblyReference.Assembly.GetReferencedAssemblies();

    [Fact]
    public void ApplicationAssembly_WhenInspected_ReferencesNoInfrastructureOrApi()
    {
        var forbidden = ApplicationReferences
            .Where(assembly => assembly.Name is "Gym.Infrastructure" or "Gym.Api")
            .Select(assembly => assembly.Name)
            .ToArray();

        forbidden.ShouldBeEmpty(
            "Application declares interfaces; Infrastructure implements them, never the other way round.");
    }

    [Fact]
    public void ApplicationAssembly_WhenInspected_ReferencesNoAspNetCoreHosting()
    {
        var webReferences = ApplicationReferences
            .Where(assembly => assembly.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .Select(assembly => assembly.Name)
            .ToArray();

        webReferences.ShouldBeEmpty(
            "use cases must stay usable without a web host, so HTTP concerns belong in Gym.Api.");
    }

    [Fact]
    public void InfrastructureAssembly_WhenInspected_ReferencesNoApi()
    {
        var forbidden = InfrastructureReferences
            .Where(assembly => assembly.Name is "Gym.Api")
            .Select(assembly => assembly.Name)
            .ToArray();

        forbidden.ShouldBeEmpty(
            "Gym.Api is the composition root: it references Infrastructure, not the reverse.");
    }
}
