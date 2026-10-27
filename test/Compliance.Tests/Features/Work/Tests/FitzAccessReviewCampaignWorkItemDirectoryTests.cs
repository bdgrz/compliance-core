using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class FitzAccessReviewCampaignWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectReviewAndRemediationAsSeparateStableWorkGivenSourceChanges()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var initialProjectionRevision = await directory.LoadRevisionAsync(fixture.TenantId);
        await ProjectAsync(directory, events, fixture.TenantId);
        Assert.Equal(initialProjectionRevision, await directory.LoadRevisionAsync(fixture.TenantId));
        var repeatedLaunchProjection = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var review = Assert.Single(repeatedLaunchProjection.Value,
            candidate => candidate.SourceId == itemId);
        var selfReviewItemId = ItemId(campaign, "rae", "admin");
        var selfReview = Assert.Single(repeatedLaunchProjection.Value, candidate =>
            candidate.SourceId == selfReviewItemId && candidate.Kind == WorkSource.AccessReviewReview);
        await fixture.SendAsync(fixture.ReviewerUserId, fixture.Decision(launched.CampaignId, itemId, 1, "revoke",
            "No longer required."));
        await ProjectAsync(directory, events, fixture.TenantId);
        var afterDecision = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var remediation = Assert.Single(afterDecision.Value,
            candidate => candidate.SourceId == itemId);
        await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Change(launched.CampaignId, itemId, 2, "ticket-42", "Disable the stale grant."));
        await ProjectAsync(directory, events, fixture.TenantId);
        var afterProviderChange = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var providerChanged = Assert.Single(afterProviderChange.Value,
            candidate => candidate.SourceId == itemId);
        var revisionAfterProviderChange = await directory.LoadRevisionAsync(fixture.TenantId);
        await ProjectAsync(directory, events, fixture.TenantId);
        var replayed = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var otherProgram = await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var otherTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(),
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal(WorkSource.AccessReviewReview, review.Kind);
        Assert.Equal("record_decision", review.NextAction);
        Assert.Equal(fixture.ReviewerMemberId, review.Responsible.Id);
        Assert.Equal(fixture.ProgramId, review.ProgramId);
        Assert.Equal(fixture.InstanceId, review.RestrictedSystemInstanceId);
        Assert.True(selfReview.RequiresSeparationOfDutiesWaiver);
        Assert.Contains(fixture.ReviewerMemberId, selfReview.Excluded);
        Assert.Equal(WorkSource.AccessReviewRemediation, remediation.Kind);
        Assert.Equal("record_remediation_change", remediation.NextAction);
        Assert.Equal(fixture.RemediationOwnerMemberId, remediation.Responsible.Id);
        Assert.NotEqual(review.WorkItemId, remediation.WorkItemId);
        Assert.Equal(remediation.WorkItemId, providerChanged.WorkItemId);
        Assert.Contains("verification", providerChanged.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("verify_remediation", providerChanged.NextAction);
        Assert.EndsWith("/remediation-verifications", providerChanged.ActionPath,
            StringComparison.Ordinal);
        Assert.Equal(revisionAfterProviderChange, await directory.LoadRevisionAsync(fixture.TenantId));
        Assert.Single(replayed.Value, candidate => candidate.WorkItemId == remediation.WorkItemId);
        Assert.Empty(otherProgram.Value);
        Assert.Empty(otherTenant.Value);
    }

    [Fact]
    public async Task ShouldRestoreRemediationWorkGivenExceptionExpiresWithoutNewEvent()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        var decision = await fixture.SendAsync(fixture.ReviewerUserId,
            fixture.Decision(launched.CampaignId, itemId, 1, "revoke", "No longer required."));
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1);
        await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Exception(launched.CampaignId, itemId, 2, "Short-lived exception while access is disabled.",
                expiresAt));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var covered = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            expiresAt.AddTicks(-1), DateOnly.MaxValue, null);
        var expired = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            expiresAt, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal("revoke", decision.Decision);
        Assert.DoesNotContain(covered.Value, candidate => candidate.SourceId == itemId);
        Assert.Equal(WorkSource.AccessReviewRemediation,
            Assert.Single(expired.Value, candidate => candidate.SourceId == itemId).Kind);
    }

    [Fact]
    public async Task ShouldRemoveRemediationWorkGivenIndependentPopulationVerification()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        var decision = await fixture.SendAsync(fixture.ReviewerUserId,
            fixture.Decision(launched.CampaignId, itemId, 1, "revoke", "No longer required."));
        var currentFacts = AccessReviewFixture.StandardFacts();
        var laterFacts = new AccessPopulationFacts(currentFacts.Principals,
            currentFacts.Entitlements,
            currentFacts.GroupMembers.Where(static membership =>
                membership.GroupProviderSubjectId != "deployer" ||
                membership.MemberProviderSubjectId != "ada").ToArray(),
            currentFacts.Assignments);
        await Task.Delay(20);
        var (laterPopulationId, _) = await fixture.AcceptAsync(laterFacts,
            DateTimeOffset.UtcNow);
        var verification = await fixture.SendAsync(fixture.ManagerUserId,
            fixture.Verification(launched.CampaignId, itemId, 2, laterPopulationId));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var candidates = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal("removed", verification.Outcome);
        Assert.DoesNotContain(candidates.Value, candidate => candidate.SourceId == itemId);
    }

    [Fact]
    public async Task ShouldReassignCurrentReviewerWithoutChangingWorkIdentityGivenEligibilityLoss()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId,
            "Reviewer delegation.");
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await ProjectAsync(directory, events, fixture.TenantId);
        var before = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var beforeCandidate = Assert.Single(before.Value, candidate => candidate.SourceId == itemId);
        var revisionBefore = await directory.LoadRevisionAsync(fixture.TenantId);
        fixture.Permissions.Deny(fixture.ApproverUserId,
            Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);
        await fixture.SendAsync(fixture.ManagerUserId, new ReassignAccessReviewResponsibility(
            Uuid.CreateVersion4(), fixture.TenantId, launched.CampaignId, itemId, AccessReviewResponsibilityKind.Reviewer,
            1, fixture.ReviewerMemberId, "Original reviewer lost Program queue access.",
            "Appointed the access owner as the review delegate."));

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var after = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var afterCandidate = Assert.Single(after.Value, candidate => candidate.SourceId == itemId);

        // Assert
        Assert.Equal(beforeCandidate.WorkItemId, afterCandidate.WorkItemId);
        Assert.Equal(fixture.ApproverMemberId, beforeCandidate.Responsible.Id);
        Assert.Equal(fixture.ReviewerMemberId, afterCandidate.Responsible.Id);
        Assert.Equal(revisionBefore + 1, await directory.LoadRevisionAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldSeparateCampaignReadVisibilityFromRemediationAuthorityGivenManagerRead()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync(applicationRestricted: true);
        fixture.AllowManagerRestrictedRead();
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.AccessControl.Services.RbacPermissions.AccessReviewManage);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var services = sourceScope.ServiceProvider;
        var eligibility = new AccessReviewQueueEligibility(
            services.GetRequiredService<IAggregateReader>(),
            new ProgramScopedQueueRead(fixture.ProgramId), fixture.Permissions,
            services.GetRequiredService<Bdgrz.Compliance.Features.Applications.Services.RestrictedApplicationVisibility>());

        // Act
        var canRead = await eligibility.CanReadAsync(fixture.TenantId, fixture.ProgramId,
            fixture.ManagerMemberId, fixture.InstanceId, CancellationToken.None);
        var canReview = await eligibility.CanReviewAsync(fixture.TenantId, fixture.ProgramId,
            fixture.ManagerMemberId, fixture.InstanceId, CancellationToken.None);
        var canRemediate = await eligibility.CanRemediateAsync(fixture.TenantId, fixture.ProgramId,
            fixture.ManagerMemberId, fixture.InstanceId, CancellationToken.None);
        var crossProgramRead = await eligibility.CanReadAsync(fixture.TenantId,
            Uuid.CreateVersion4(), fixture.ManagerMemberId, fixture.InstanceId,
            CancellationToken.None);

        // Assert
        Assert.True(canRead);
        Assert.True(canReview);
        Assert.False(canRemediate);
        Assert.False(crossProgramRead);
    }

    [Fact]
    public async Task ShouldExcludeUnroutedLegacyCampaignGivenTenantDirectLaunch()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var routed = await fixture.LaunchAsync(populationId);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await ProjectAsync(directory, events, fixture.TenantId);
        var routedCandidates = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var (legacyCampaignId, _) = await fixture.SeedLegacyCampaignAsync(routed.CampaignId);

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var candidates = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);

        // Assert
        Assert.NotEqual(routed.CampaignId, legacyCampaignId);
        Assert.Equal(routedCandidates.Value.Select(static candidate => candidate.WorkItemId)
                .Order().ToArray(),
            candidates.Value.Select(static candidate => candidate.WorkItemId).Order().ToArray());
    }

    [Fact]
    public async Task ShouldRejectPartialFrozenRouteWithoutAdvancingCheckpointGivenMalformedCampaign()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var routed = await fixture.LaunchAsync(populationId);
        var source = await fixture.CampaignAsync(routed.CampaignId);
        await AccessReviewCampaignFacts.SeedLaunchedAsync(fixture.Provider, fixture.TenantId, Uuid.CreateVersion4(),
            source.Reviewers, source.Items.Select(static item => item.Item).ToArray(), source.LaunchedBy,
            source.LaunchedAt, fixture.ProgramId, null);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var before = await directory.LoadCheckpointAsync(fixture.TenantId);

        // Act
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ProjectAsync(directory, events, fixture.TenantId));
        var after = await directory.LoadCheckpointAsync(fixture.TenantId);

        // Assert
        Assert.Contains("both its Program and remediation owner", error.Message,
            StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    sealed class ProgramScopedQueueRead(Uuid permittedProgramId)
        : Bdgrz.Compliance.Features.AccessControl.Authorizers.IAccessGrantPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(programId == permittedProgramId &&
                permission == Bdgrz.Compliance.Features.Programs.Contracts.IProgramReadRequest.ReadPermission);

        public ValueTask<Bdgrz.Compliance.Features.AccessControl.Readers.ProgramAccessVisibility>
            GetProgramVisibilityAsync(Uuid tenantId, Uuid userId, Uuid memberId,
                string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new Bdgrz.Compliance.Features.AccessControl.Readers.ProgramAccessVisibility(
                false, new HashSet<Uuid> { permittedProgramId }));
    }

    static FitzAccessReviewCampaignWorkItemDirectory CreateDirectory(IServiceProvider services) =>
        new(new InMemoryKvClient(), services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<Bdgrz.Compliance.Features.Content.Contracts.IImmutableContentStore>(),
            services.GetRequiredService<IDomainEventReader>());

    static Uuid ItemId(AccessReviewCampaignView campaign, string subject, string entitlement) =>
        Assert.Single(campaign.Items, item => item.Item.ProviderSubjectId == subject &&
            item.Item.ProviderEntitlementId == entitlement).Item.ItemId;

    static async Task ProjectAsync(FitzAccessReviewCampaignWorkItemDirectory directory,
        IDomainEventReader events, Uuid tenantId)
    {
        var pattern = directory.SourcePattern(tenantId);
        var identity = new CheckpointIdentity(
            FitzAccessReviewCampaignWorkItemDirectory.ProjectorName, pattern);
        var current = await directory.LoadCheckpointAsync(tenantId);
        var cursor = current.Cursor;
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            current));
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}
