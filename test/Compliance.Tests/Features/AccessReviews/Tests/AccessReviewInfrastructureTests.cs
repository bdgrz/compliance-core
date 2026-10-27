using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Reactors;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Authorizers;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessReviewInfrastructureTests
{
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Actor");

    [Fact]
    public async Task ShouldProjectPopulationStatusIdempotentlyGivenReplayedEvents()
    {
        // Arrange
        var directory = new FitzAccessPopulationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var stream = new EventStreamAddress(tenantId.ToString(), AccessPopulation.Area, populationId.ToString());
        var opened = new AccessPopulationOpened(tenantId, populationId, Uuid.CreateVersion4(),
            instanceId, 1, DateTimeOffset.UtcNow.AddDays(-1), "manual_attestation", "Export", Actor,
            DateTimeOffset.UtcNow, AccessReviewFacts.Provenance(stream, "OpenAccessPopulation", populationId),
            AccessReviewFacts.Response(), AccessReviewFacts.Response());
        var recorded = new AccessPopulationFactsRecorded(tenantId, populationId, 2,
            AccessReviewFacts.Response(), Actor, DateTimeOffset.UtcNow,
            AccessReviewFacts.Provenance(stream, "RecordAccessPopulationFacts"), AccessReviewFacts.Response(),
            AccessReviewFacts.Response());
        var accepted = new AccessPopulationAccepted(tenantId, populationId, 3, Uuid.CreateVersion4(),
            new string('a', 64), Uuid.CreateVersion4(), "Attested.", Actor, DateTimeOffset.UtcNow,
            AccessReviewFacts.Provenance(stream, "AcceptAccessPopulation"), AccessReviewFacts.Response(),
            AccessReviewFacts.Response());

        // Act
        await ProjectAsync(directory, tenantId, opened, recorded, accepted, opened, recorded, accepted);
        var page = await directory.ListAsync(tenantId, instanceId, 10, null);
        var otherTenant = await directory.ListAsync(Uuid.CreateVersion4(), instanceId, 10, null);

        // Assert
        var summary = Assert.Single(page.Items);
        Assert.Equal("accepted", summary.Status);
        Assert.Equal(3, summary.Revision);
        Assert.Equal(accepted.SnapshotId, summary.SnapshotId);
        Assert.Empty(otherTenant.Items);
    }

    [Fact]
    public async Task ShouldRejectOutOfOrderProjectionGivenChangeBeforeOpen()
    {
        // Arrange
        var directory = new FitzAccessPopulationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var stream = new EventStreamAddress(tenantId.ToString(), AccessPopulation.Area, populationId.ToString());
        var recorded = new AccessPopulationFactsRecorded(tenantId, populationId, 2,
            AccessReviewFacts.Response(), Actor, DateTimeOffset.UtcNow,
            AccessReviewFacts.Provenance(stream, "RecordAccessPopulationFacts"), AccessReviewFacts.Response(),
            AccessReviewFacts.Response());

        // Act
        var failure = await Record.ExceptionAsync(() => ProjectAsync(directory, tenantId, recorded));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public async Task ShouldAllowParticipantButRequirePermissionGivenManagementRequest()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var permissions = new RecordingPermissionAuthorizer(false);
        var authorizer = new AccessReviewAuthorizer(new FixedMembershipDirectory(true),
            new ActiveTenant(), permissions);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

        // Act
        var participant = await authorizer.AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
            new GetAccessReviewCampaign(tenantId, Uuid.CreateVersion4()), actor), CancellationToken.None);
        var management = await authorizer.AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
            new LaunchAccessReviewCampaign(Uuid.CreateVersion4(), tenantId, "Review", "Do it",
                DateTimeOffset.UtcNow.AddDays(1), []),
            actor), CancellationToken.None);
        var firmStaff = await new AccessReviewAuthorizer(new FixedMembershipDirectory(true, "firm_staff"),
                new ActiveTenant(), permissions)
            .AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
                new GetAccessReviewCampaign(tenantId, Uuid.CreateVersion4()), actor), CancellationToken.None);
        var deprovisioned = await new AccessReviewAuthorizer(new FixedMembershipDirectory(true,
                isDeprovisioned: true), new ActiveTenant(), permissions)
            .AuthorizeAsync(new RequestContext<IAccessReviewRequest>(
                new GetAccessReviewCampaign(tenantId, Uuid.CreateVersion4()), actor), CancellationToken.None);

        // Assert
        Assert.True(participant.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(management.Error).Kind);
        Assert.Equal(RbacPermissions.AccessReviewManage, Assert.Single(permissions.Permissions));
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(firmStaff.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(deprovisioned.Error).Kind);
    }

    [Fact]
    public async Task ShouldGrantAccessReviewPermissionGivenTenantRegistration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(
            new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme"));

        // Act
        await scenario.RunAsync(new TenantRbacBootstrapReactor(
            new InMemoryProjectionCheckpointStore(), TypedReactionBus.For(scenario)));

        // Assert
        var grants = TypedReactionBus.Sent(scenario).OfType<AssignRolePermission>()
            .Where(grant => grant.Permission == RbacPermissions.AccessReviewManage).ToArray();
        Assert.Equal(2, grants.Length);
        Assert.Equal(new[] { BuiltInRbac.TenantAdministrationRoleId(tenantId),
            BuiltInRbac.ComplianceManagementRoleId(tenantId) }.Order(), grants.Select(grant => grant.RoleId).Order());
    }

    static async Task ProjectAsync(FitzAccessPopulationDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        var identity = new CheckpointIdentity("AccessPopulationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString(), "access-populations"));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var ev in events)
            await directory.ApplyAsync(ev);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
