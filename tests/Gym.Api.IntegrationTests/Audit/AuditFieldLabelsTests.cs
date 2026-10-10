using System.Text.Json;

using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Domain.Audit;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Interceptors;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Audit;

/// <summary>
/// Every kind of record and every field the audit log can write has a Persian name on the audit
/// screen (BUSINESS_RULES.md §11 <i>The audit screen</i>).
/// </summary>
/// <remarks>
/// The names live in the frontend, in <c>web/src/features/audit/auditFields.json</c>; this test
/// reads that file and the EF Core model, so a new entity or property fails here, not as an English
/// word on the Owner's screen. The page still shows the English name if one slips through.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AuditFieldLabelsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string LabelsFile = "web/src/features/audit/auditFields.json";

    [Fact]
    public void Labels_EveryAuditedEntity_HasAPersianName()
    {
        var labels = ReadLabels();

        var missing = AuditedEntities()
            .Select(entity => entity.DisplayName())
            .Where(name => !labels.RootElement.GetProperty("entities").TryGetProperty(name, out _))
            .ToList();

        missing.ShouldBeEmpty($"add a Persian name for each of these to \"entities\" in {LabelsFile}.");
    }

    [Fact]
    public void Labels_EveryAuditedField_HasALabelOrIsHidden()
    {
        var labels = ReadLabels();
        var fields = labels.RootElement.GetProperty("fields");
        var entityFields = labels.RootElement.GetProperty("entityFields");

        var missing = AuditedEntities()
            .SelectMany(entity => entity.GetProperties().Select(property => (Entity: entity.DisplayName(), Field: property.Name)))
            .Where(pair => !AuditLogInterceptor.ExcludedProperties.Contains(pair.Field))
            .Where(pair =>
                !fields.TryGetProperty(pair.Field, out _) &&
                !(entityFields.TryGetProperty(pair.Entity, out var own) && own.TryGetProperty(pair.Field, out _)))
            .Select(pair => $"{pair.Entity}.{pair.Field}")
            .ToList();

        missing.ShouldBeEmpty($"add a label (or \"format\": \"hidden\") for each of these to \"fields\" in {LabelsFile}.");
    }

    /// <summary>What the interceptor audits: every entity but the log itself and Identity's token table.</summary>
    private List<Microsoft.EntityFrameworkCore.Metadata.IEntityType> AuditedEntities()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return db.Model.GetEntityTypes()
            .Where(entity => entity.ClrType != typeof(AuditLog) && entity.ClrType != typeof(IdentityUserToken<Guid>))
            .ToList();
    }

    private static JsonDocument ReadLabels() =>
        JsonDocument.Parse(RepositoryFiles.ReadAllText(LabelsFile));
}
