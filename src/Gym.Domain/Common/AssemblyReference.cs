using System.Reflection;

namespace Gym.Domain.Common;

/// <summary>
/// Anchor type for assembly-level reflection: architecture tests today, and later any
/// DI or validator scanning that needs to name this assembly without a magic string.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
