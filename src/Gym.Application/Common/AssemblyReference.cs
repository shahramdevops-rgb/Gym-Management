using System.Reflection;

namespace Gym.Application.Common;

/// <summary>
/// Anchor type for assembly-level reflection: architecture tests today, and later the
/// FluentValidation and handler registration scans in <c>AddApplication()</c>.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
