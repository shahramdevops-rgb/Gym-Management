using Gym.Domain.Common;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Gym.Api.IntegrationTests.Persistence;

/// <summary>
/// Conventions that must hold for every entity the project will ever add, asserted against
/// the stand-in entity while the real model is still empty. Building a model does not open a
/// connection, so these run without Docker.
/// </summary>
public sealed class AppDbContextModelTests
{
    private static readonly IEntityType TestEntityType = GetTestEntityType();

    [Theory]
    [InlineData(nameof(Entity.Id), "id")]
    [InlineData(nameof(Entity.CreatedAt), "created_at")]
    [InlineData(nameof(Entity.CreatedBy), "created_by")]
    [InlineData(nameof(Entity.UpdatedAt), "updated_at")]
    [InlineData(nameof(Entity.UpdatedBy), "updated_by")]
    [InlineData(nameof(TestEntity.DisplayName), "display_name")]
    public void Model_WhenBuilt_MapsPropertiesToSnakeCaseColumns(string property, string column)
    {
        GetProperty(property)
            .GetColumnName()
            .ShouldBe(
                column,
                "Postgres folds unquoted identifiers to lower case, so snake_case column " +
                "names keep SQL readable without quoting every identifier by hand.");
    }

    [Fact]
    public void Model_WhenBuilt_MapsTheEntityToASnakeCaseTable()
    {
        TestEntityType.GetTableName().ShouldBe("test_entities");
    }

    [Theory]
    [InlineData(nameof(Entity.CreatedAt))]
    [InlineData(nameof(Entity.UpdatedAt))]
    public void Model_WhenBuilt_StoresMomentsAsTimestamptz(string property)
    {
        GetProperty(property)
            .GetColumnType()
            .ShouldBe(
                "timestamp with time zone",
                "moments are absolute points in time; timestamp without time zone would " +
                "silently drop the offset and make 'when did this happen' unanswerable.");
    }

    [Fact]
    public void Model_WhenBuilt_UsesTheIdAsPrimaryKey()
    {
        var key = TestEntityType.FindPrimaryKey().ShouldNotBeNull();

        key.Properties.Select(property => property.Name).ShouldBe([nameof(Entity.Id)]);
    }

    [Theory]
    [InlineData(nameof(Entity.CreatedAt))]
    [InlineData(nameof(Entity.CreatedBy))]
    [InlineData(nameof(Entity.UpdatedAt))]
    [InlineData(nameof(Entity.UpdatedBy))]
    public void Model_WhenBuilt_MapsTheAuditFieldsDespiteTheirPrivateSetters(string property)
    {
        // The audit fields have no public setter, which is what lets Entity stay encapsulated.
        // EF maps them anyway, and the interceptor writes through the change tracker; if that
        // ever stopped working, the columns would quietly vanish from the schema instead.
        GetProperty(property).ShouldNotBeNull();
    }

    [Fact]
    public void AppDbContext_WhenInspected_ImplementsTheApplicationContract()
    {
        // The seam that keeps handlers off the concrete context.
        typeof(Gym.Application.Common.IAppDbContext)
            .IsAssignableFrom(typeof(AppDbContext))
            .ShouldBeTrue();
    }

    private static IProperty GetProperty(string name) =>
        TestEntityType.FindProperty(name)
            ?? throw new InvalidOperationException($"'{name}' is not mapped.");

    private static IEntityType GetTestEntityType()
    {
        using var context = PersistenceTestContext.Create();

        return context.Model.FindEntityType(typeof(TestEntity))
            ?? throw new InvalidOperationException("TestEntity is not in the model.");
    }
}
