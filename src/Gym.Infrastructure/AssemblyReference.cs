using System.Reflection;

namespace Gym.Infrastructure;

/// <summary>
/// Anchor type for assembly-level reflection: architecture tests today, and later the
/// EF Core configuration scan in <c>AddInfrastructure()</c>.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
