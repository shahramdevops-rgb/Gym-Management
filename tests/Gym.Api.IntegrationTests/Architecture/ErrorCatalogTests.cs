using System.Reflection;

using FluentValidation;

using Gym.Api.Authorization;
using Gym.Api.Common;
using Gym.Domain.Common;

namespace Gym.Api.IntegrationTests.Architecture;

/// <summary>
/// Every error code the API can send has a Persian message in <c>web/src/lib/errors.ts</c>.
/// </summary>
/// <remarks>
/// The two halves live in different languages, so no compiler connects them. Without this, a
/// new <c>Members.PhoneAlreadyExists</c> would reach the front desk as a generic "unexpected
/// error", and nobody would notice until a user did. The codes are collected by reflection from
/// the places the API defines them, so a new error is covered the moment it exists.
/// </remarks>
public sealed class ErrorCatalogTests
{
    private static readonly Assembly[] ErrorAssemblies =
    [
        Gym.Domain.Common.AssemblyReference.Assembly,
        Gym.Application.Common.AssemblyReference.Assembly,
    ];

    [Fact]
    public void ErrorCodes_DefinedAnywhereInTheApi_HaveAPersianMessageInTheFrontend()
    {
        var catalogue = File.ReadAllText(FrontendErrorsFile());

        var codes = DomainAndApplicationErrors()
            .Concat(ValidatorErrorCodes())
            .Concat(ApiLevelCodes())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        codes.Count.ShouldBeGreaterThan(20, "the collection below proves nothing if it found no codes.");

        var missing = codes.Where(code => !catalogue.Contains($"\"{code}\":", StringComparison.Ordinal)).ToList();

        missing.ShouldBeEmpty("add a Persian message for each of these codes to web/src/lib/errors.ts.");
    }

    /// <summary>
    /// <c>public static readonly Error</c> fields, and static methods that build an
    /// <see cref="Error"/> from a description (such as <c>StaffErrors.Rejected</c>).
    /// </summary>
    private static IEnumerable<string> DomainAndApplicationErrors()
    {
        var types = ErrorAssemblies.SelectMany(assembly => assembly.GetTypes());

        foreach (var type in types)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(field => field.FieldType == typeof(Error)))
            {
                yield return ((Error)field.GetValue(null)!).Code;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                         .Where(method => method.ReturnType == typeof(Error) &&
                             method.DeclaringType != typeof(Error) &&
                             method.GetParameters() is [{ ParameterType: var parameter }] && parameter == typeof(string)))
            {
                yield return ((Error)method.Invoke(null, ["description"])!).Code;
            }
        }
    }

    /// <summary>Every <c>WithErrorCode</c> in every validator in Application.</summary>
    private static IEnumerable<string> ValidatorErrorCodes()
    {
        var validatorTypes = Gym.Application.Common.AssemblyReference.Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(IValidator).IsAssignableFrom(type));

        foreach (var type in validatorTypes)
        {
            var validator = (IValidator)Activator.CreateInstance(type)!;

            foreach (var rule in validator.CreateDescriptor().Rules)
            {
                foreach (var component in rule.Components)
                {
                    if (!string.IsNullOrEmpty(component.ErrorCode))
                    {
                        yield return component.ErrorCode;
                    }
                }
            }
        }
    }

    /// <summary>Codes raised by the API layer itself: validation, rate limiting, authorization.</summary>
    private static IEnumerable<string> ApiLevelCodes() =>
        new[] { typeof(ApiErrorCodes), typeof(ProblemDetailsAuthorizationResultHandler) }
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);

    private static string FrontendErrorsFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "web", "src", "lib", "errors.ts");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("web/src/lib/errors.ts was not found above the test output directory.");
    }
}
