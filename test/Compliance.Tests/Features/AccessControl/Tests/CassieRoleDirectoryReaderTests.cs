using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

/// <summary>
///     Exercises native Cassie ordering, cursor, search, and isolation through the production repository.
/// </summary>
[Trait("Category", "CassieIntegration")]
public sealed class CassieRoleDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnStoredRoleGivenMatchingId()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var roleId = Uuid.CreateVersion4();
        await SeedAsync(database.Database, roleId, "Reviewer");
        await using var reader = new CassieRoleDirectoryReader(database.Database);

        // Act
        var role = await reader.GetAsync(TenantId, roleId, CancellationToken.None);

        // Assert
        Assert.NotNull(role);
        Assert.Equal("Reviewer", role.Name);
    }

    [Fact]
    public async Task ShouldReturnNullGivenNoSuchRole()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await using var reader = new CassieRoleDirectoryReader(database.Database);

        // Act
        var role = await reader.GetAsync(TenantId, Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.Null(role);
    }

    [Fact]
    public async Task ShouldReplaceDisplayNameGivenRoleRenameEvent()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var roleId = Uuid.CreateVersion4();
        await SeedAsync(database.Database, roleId, "Tenant Administration");
        await using var reader = new CassieRoleDirectoryReader(database.Database);
        var identity = new CheckpointIdentity("RoleDirectory", EventStreamPattern.ForPattern(TenantId.ToString()));
        await using (var batch = await reader.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await reader.ApplyAsync(RbacFacts.RoleRenamedFact(TenantId, roleId, "Org Admin"));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var role = await reader.GetAsync(TenantId, roleId, CancellationToken.None);

        // Assert
        Assert.NotNull(role);
        Assert.Equal("Org Admin", role.Name);
    }

    [Fact]
    public async Task ShouldReturnRolesInNameOrderGivenTenantQuery()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await SeedAsync(database.Database, Uuid.CreateVersion4(), "Beta");
        await SeedAsync(database.Database, Uuid.CreateVersion4(), "Alpha");
        await using var reader = new CassieRoleDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(TenantId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        Assert.Equal(["Alpha", "Beta"], page.Items.Select(role => role.Name));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ShouldFilterRolesGivenNameSubstring()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        await SeedAsync(database.Database, Uuid.CreateVersion4(), "Site Reviewer");
        await SeedAsync(database.Database, Uuid.CreateVersion4(), "Administrator");
        await using var reader = new CassieRoleDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(TenantId, null, null, "review", descending: false, CancellationToken.None);

        // Assert
        var role = Assert.Single(page.Items);
        Assert.Equal("Site Reviewer", role.Name);
    }

    [Fact]
    public async Task ShouldReturnRolesOnlyGivenRequestedTenant()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieAccessControlSchema.Statements);
        var otherTenantId = Uuid.CreateVersion4();
        await SeedAsync(database.Database, Uuid.CreateVersion4(), "Mine", TenantId);
        await SeedAsync(database.Database, Uuid.CreateVersion4(), "Theirs", otherTenantId);
        await using var reader = new CassieRoleDirectoryReader(database.Database);

        // Act
        var page = await reader.ListAsync(TenantId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var role = Assert.Single(page.Items);
        Assert.Equal("Mine", role.Name);
    }

    static async Task SeedAsync(CassieProjectionDatabase database, Uuid roleId, string name, Uuid? tenantId = null)
    {
        var tenant = tenantId ?? TenantId;
        await using var repository = new CassieRoleDirectoryReader(database);
        var identity = new CheckpointIdentity("RoleDirectory", EventStreamPattern.ForPattern(tenant.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(RbacFacts.RoleDefinedFact(tenant, roleId, name));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
