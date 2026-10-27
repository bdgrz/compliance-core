using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
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
///     Exercises the real KvDirectory-backed index/cursor/search logic against Fitz's own in-memory
///     KV double — same coverage as <see cref="FitzTeamDirectoryReaderTests" />.
/// </summary>
public sealed class FitzRoleDirectoryReaderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldReturnStoredRoleGivenMatchingId()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var roleId = Uuid.CreateVersion4();
        await SeedAsync(client, roleId, "Reviewer");
        var reader = new FitzRoleDirectoryReader(client);

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
        var reader = new FitzRoleDirectoryReader(new InMemoryKvClient());

        // Act
        var role = await reader.GetAsync(TenantId, Uuid.CreateVersion4(), CancellationToken.None);

        // Assert
        Assert.Null(role);
    }

    [Fact]
    public async Task ShouldReplaceDisplayNameGivenRoleRenameEvent()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var roleId = Uuid.CreateVersion4();
        await SeedAsync(client, roleId, "Tenant Administration");
        var reader = new FitzRoleDirectoryReader(client);
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
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Beta");
        await SeedAsync(client, Uuid.CreateVersion4(), "Alpha");
        var reader = new FitzRoleDirectoryReader(client);

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
        var client = new InMemoryKvClient();
        await SeedAsync(client, Uuid.CreateVersion4(), "Site Reviewer");
        await SeedAsync(client, Uuid.CreateVersion4(), "Administrator");
        var reader = new FitzRoleDirectoryReader(client);

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
        var client = new InMemoryKvClient();
        var otherTenantId = Uuid.CreateVersion4();
        await SeedAsync(client, Uuid.CreateVersion4(), "Mine", TenantId);
        await SeedAsync(client, Uuid.CreateVersion4(), "Theirs", otherTenantId);
        var reader = new FitzRoleDirectoryReader(client);

        // Act
        var page = await reader.ListAsync(TenantId, null, null, null, descending: false, CancellationToken.None);

        // Assert
        var role = Assert.Single(page.Items);
        Assert.Equal("Mine", role.Name);
    }

    static async Task SeedAsync(InMemoryKvClient client, Uuid roleId, string name, Uuid? tenantId = null)
    {
        var tenant = tenantId ?? TenantId;
        var repository = new FitzRoleDirectoryReader(client);
        var identity = new CheckpointIdentity("RoleDirectory", EventStreamPattern.ForPattern(tenant.ToString()));
        await using var batch = await repository.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await repository.ApplyAsync(RbacFacts.RoleDefinedFact(tenant, roleId, name));
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
