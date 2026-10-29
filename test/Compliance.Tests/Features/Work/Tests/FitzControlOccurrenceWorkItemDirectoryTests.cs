using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Tests;
using Bdgrz.Compliance.Features.Boundaries.Readers;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class FitzControlOccurrenceWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldReturnNoOccurrenceWorkGivenInitialPendingPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.ProposeAsync(0);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(),
                sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId, today,
            today.AddDays(30), null, CancellationToken.None);

        // Assert
        Assert.True(work.IsSuccess);
        Assert.Empty(work.Value);
    }

    [Fact]
    public async Task ShouldMatchLedgerAfterReassigningOpenEventDrivenOccurrenceGivenReplacementPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var cadence = new ControlCadence("ad_hoc", DueWithinDays: 10);
        await fixture.PlanAsync(cadence: cadence);
        var opened = await fixture.AsAsync(fixture.OwnerUserId, fixture.Open("Quarterly access review", fixture.Today));
        var replacementOwner = new OperatingHolder(OperatingAuthority.MemberHolder,
            fixture.BackupMemberId);
        await fixture.PlanAsync(owner: replacementOwner, cadence: cadence,
            effectiveFrom: fixture.Today);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);
        var ledgerWork = await WorkSource.LoadAsync(sourceReader, sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), null, sourceScope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(), sourceScope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlOperationsReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlMappingReader>(), sourceScope.ServiceProvider.GetRequiredService<ApplicabilityReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlEvaluationReader>(), fixture.TenantId, fixture.ProgramId, today, horizon, now, null, fixture.Boundaries, new HashSet<string>(StringComparer.Ordinal), CancellationToken.None, sourceScope.ServiceProvider.GetRequiredService<BoundaryReader>());
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);

        // Act
        var projectedWork = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(ledgerWork.IsSuccess);
        Assert.True(projectedWork.IsSuccess);
        AssertEquivalentCandidates(ledgerWork.Value.Where(IsOccurrenceWork).ToArray(),
            projectedWork.Value);
        var candidate = Assert.Single(projectedWork.Value,
            item => item.Kind == WorkSource.ControlOccurrence && item.SourceId == opened.OccurrenceId);
        Assert.Equal(replacementOwner, candidate.Responsible);
    }

    [Fact]
    public async Task ShouldMatchLedgerOccurrenceCandidatesGivenApprovedPlanAndHorizon()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var ledgerWork = await WorkSource.LoadAsync(sourceReader, sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), null, sourceScope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(), sourceScope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlOperationsReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlMappingReader>(), sourceScope.ServiceProvider.GetRequiredService<ApplicabilityReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlEvaluationReader>(), fixture.TenantId, fixture.ProgramId, today, horizon, now, null, fixture.Boundaries, new HashSet<string>(StringComparer.Ordinal), CancellationToken.None, sourceScope.ServiceProvider.GetRequiredService<BoundaryReader>());
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);

        // Act
        var projectedWork = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(ledgerWork.IsSuccess);
        Assert.True(projectedWork.IsSuccess);
        AssertEquivalentCandidates(ledgerWork.Value.Where(IsOccurrenceWork).ToArray(),
            projectedWork.Value);
    }

    [Fact]
    public async Task ShouldMatchLedgerAcrossRecurringPlanHistoryGivenReplacementCadence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var replacementCadence = new ControlCadence("recurring", "quarterly",
            fixture.Today.AddMonths(1), 8);
        await fixture.PlanAsync(cadence: replacementCadence, effectiveFrom: fixture.Today);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(120);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var ledgerWork = await WorkSource.LoadAsync(sourceReader, sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), null, sourceScope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(), sourceScope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlOperationsReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlMappingReader>(), sourceScope.ServiceProvider.GetRequiredService<ApplicabilityReader>(), sourceScope.ServiceProvider.GetRequiredService<ControlEvaluationReader>(), fixture.TenantId, fixture.ProgramId, today, horizon, now, null, fixture.Boundaries, new HashSet<string>(StringComparer.Ordinal), CancellationToken.None, sourceScope.ServiceProvider.GetRequiredService<BoundaryReader>());
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);

        // Act
        var projectedWork = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(ledgerWork.IsSuccess);
        Assert.True(projectedWork.IsSuccess);
        AssertEquivalentCandidates(ledgerWork.Value.Where(IsOccurrenceWork).ToArray(),
            projectedWork.Value);
        Assert.Contains(projectedWork.Value, item => item.Kind == WorkSource.ControlOccurrence &&
            item.DueOn > fixture.Today.AddDays(20));
    }

    [Fact]
    public async Task ShouldProjectPendingReviewAndReturnedOccurrenceGivenAttestationAndDecision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync())
            .First(item => item.PeriodStart <= fixture.Today);
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);

        // Act
        var submitted = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);
        await fixture.AsAsync(fixture.ReviewerUserId, fixture.Review(attested, "returned"));
        await CatchUpAsync(fixture, directory);
        var returned = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(submitted.IsSuccess);
        var review = Assert.Single(submitted.Value,
            item => item.Kind == WorkSource.OccurrenceReview);
        Assert.Equal(occurrence.OccurrenceId, review.SourceId);
        Assert.Equal("review", review.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{fixture.ControlId}/occurrences/{occurrence.OccurrenceId}/reviews",
            review.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder,
            plan.ReviewerMemberId), review.Responsible);
        Assert.Contains(fixture.OwnerMemberId, review.Excluded);
        Assert.True(returned.IsSuccess);
        Assert.DoesNotContain(returned.Value,
            item => item.Kind == WorkSource.OccurrenceReview &&
                    item.SourceId == occurrence.OccurrenceId);
        var returnedOccurrence = Assert.Single(returned.Value,
            item => item.Kind == WorkSource.ControlOccurrence &&
                    item.SourceId == occurrence.OccurrenceId);
        Assert.Equal("attest", returnedOccurrence.NextAction);
        Assert.Equal(occurrence.DueOn, returnedOccurrence.DueOn);
    }

    [Fact]
    public async Task ShouldKeepReviewWorkGivenDeferredDecision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync())
            .First(item => item.PeriodStart <= fixture.Today);
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        var deferred = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(attested, "deferred"));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId, today,
            today.AddDays(30), null, CancellationToken.None);

        // Assert
        Assert.Equal("deferred", deferred.State);
        Assert.True(work.IsSuccess);
        var review = Assert.Single(work.Value, item =>
            item.Kind == WorkSource.OccurrenceReview && item.SourceId == occurrence.OccurrenceId);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder,
            plan.ReviewerMemberId), review.Responsible);
        Assert.Contains(fixture.OwnerMemberId, review.Excluded);
    }

    [Fact]
    public async Task ShouldProjectLatestCorrectionAsPendingReviewGivenReturnedAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync(backup: new OperatingHolder(
            OperatingAuthority.MemberHolder, fixture.BackupMemberId));
        var occurrence = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        var returned = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "returned"));
        var corrected = await fixture.AsAsync(fixture.BackupUserId,
            new CorrectControlAttestation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                returned.OccurrenceId, returned.Revision, "complete",
                DateTimeOffset.UtcNow.AddMinutes(-1), null, null, "Correction recorded.", null,
                OperationsFixture.FullSupport, "Included the missing support."));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(), sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId, today,
            today.AddDays(30), null, CancellationToken.None);

        // Assert
        Assert.Equal(2, corrected.Attestations.Count);
        Assert.Equal(corrected.Attestations[0].AttestationId,
            corrected.Attestations[1].SupersedesAttestationId);
        Assert.True(work.IsSuccess);
        var review = Assert.Single(work.Value, item =>
            item.Kind == WorkSource.OccurrenceReview && item.SourceId == occurrence.OccurrenceId);
        Assert.Equal(plan.ReviewerMemberId, review.Responsible.Id);
        Assert.Equal(corrected.Attestations[^1].RecordedAt, review.CreatedAt);
        Assert.Contains(fixture.BackupMemberId, review.Excluded);
        Assert.DoesNotContain(fixture.OwnerMemberId, review.Excluded);
    }

    [Fact]
    public async Task ShouldIsolateProjectedOccurrencesToTheirTenantAndProgramGivenPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(),
                sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await CatchUpAsync(fixture, directory);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);

        // Act
        var otherProgram = await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), today, horizon, null, CancellationToken.None);
        var otherTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(), fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(otherProgram.IsSuccess);
        Assert.Empty(otherProgram.Value);
        Assert.True(otherTenant.IsSuccess);
        Assert.Empty(otherTenant.Value);
    }

    internal static async Task CatchUpAsync(OperationsFixture fixture,
        FitzControlOccurrenceWorkItemDirectory directory)
    {
        var pattern = directory.SourcePattern(fixture.TenantId);
        var checkpoint = await directory.LoadCheckpointAsync(fixture.TenantId);
        var identity = new CheckpointIdentity(FitzControlOccurrenceWorkItemDirectory.ProjectorName,
            pattern);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            checkpoint));
        var cursor = checkpoint.Cursor;
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            if (record.Event is ControlOperatingPlanProposed or ControlOperatingPlanApproved or
                ControlOccurrenceOpened or ControlOccurrenceAttested or ControlOccurrenceReviewed)
                await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static bool IsOccurrenceWork(WorkCandidate candidate) =>
        candidate.Kind is WorkSource.ControlOccurrence or WorkSource.OccurrenceReview;

    static void AssertEquivalentCandidates(WorkCandidate[] expected,
        IReadOnlyList<WorkCandidate> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        var actualById = actual.ToDictionary(static candidate => candidate.WorkItemId);
        foreach (var candidate in expected)
        {
            Assert.True(actualById.TryGetValue(candidate.WorkItemId, out var projected));
            Assert.Equal(candidate.Kind, projected!.Kind);
            Assert.Equal(candidate.SourceId, projected.SourceId);
            Assert.Equal(candidate.ControlId, projected.ControlId);
            Assert.Equal(candidate.Summary, projected.Summary);
            Assert.Equal(candidate.Reason, projected.Reason);
            Assert.Equal(candidate.DueOn, projected.DueOn);
            Assert.Equal(candidate.NextAction, projected.NextAction);
            Assert.Equal(candidate.ActionPath, projected.ActionPath);
            Assert.Equal(candidate.Responsible, projected.Responsible);
            Assert.Equal(candidate.Backup, projected.Backup);
            Assert.Equal(candidate.Excluded.OrderBy(static member => member.ToString()),
                projected.Excluded.OrderBy(static member => member.ToString()));
            Assert.Equal(candidate.CreatedAt, projected.CreatedAt);
        }
    }
}
