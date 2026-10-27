using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Handlers;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class ControlOperatingHistoryCompactionTests
{
    static readonly ControlCadence AdHoc = new("ad_hoc", DueWithinDays: 10);

    [Fact]
    public async Task ShouldKeepPlanCommandAuthorityBoundedGivenManyApprovedVersionsWithFullReadHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        for (var index = 0; index < 24; index++)
            await fixture.PlanAsync(cadence: AdHoc, effectiveFrom: fixture.ControlEffectiveFrom.AddDays(index));

        // Act
        var owner = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        var plans = await fixture.GetPlansAsync();

        // Assert
        Assert.Equal(48, owner.Revision);
        Assert.Equal(24, plans.History.Count);
        Assert.Equal(23, plans.History.Count(static plan => plan.Status == "superseded"));
        Assert.Equal(plans.History[^1].PlanVersionId, plans.Current!.PlanVersionId);
        AssertBounded(owner);
    }

    [Fact]
    public async Task ShouldKeepOccurrenceCommandAuthorityBoundedGivenManyCorrectedAndReviewedVersions()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await fixture.PlanAsync(cadence: AdHoc);
        var opened = await fixture.AsAsync(fixture.OwnerUserId, fixture.Open("Release performance", fixture.Today));
        var current = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(opened));
        for (var index = 0; index < 24; index++)
        {
            current = await fixture.AsAsync(fixture.OwnerUserId, Correction(fixture, current));
            current = await fixture.AsAsync(fixture.ReviewerUserId, fixture.Review(current, "returned"));
        }

        // Act
        var owner = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOccurrence(fixture.TenantId, fixture.ProgramId, fixture.ControlId, opened.OccurrenceId));
        var view = await fixture.GetOccurrenceAsync(opened.OccurrenceId);

        // Assert
        Assert.Equal(50, owner.Revision);
        Assert.Equal(25, owner.AttestationCount);
        Assert.Equal(25, view.Attestations.Count);
        Assert.Equal(24, view.Reviews.Count);
        Assert.Equal("returned", view.State);
        AssertBounded(owner);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("attest")]
    [InlineData("correct")]
    [InlineData("review")]
    public async Task ShouldReplayOriginalOccurrenceResultGivenLaterHolderOrReviewerReassignment(string operation)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await fixture.PlanAsync(cadence: AdHoc);
        var open = fixture.Open("Release performance", fixture.Today);
        var opened = await fixture.AsAsync(fixture.OwnerUserId, open);
        IRequest<ControlOccurrenceView> request = open;
        var actor = fixture.OwnerUserId;
        var first = opened;
        if (operation != "open")
        {
            var attest = fixture.Attest(opened);
            first = await fixture.AsAsync(fixture.OwnerUserId, attest);
            request = attest;
            if (operation == "correct")
            {
                var correction = Correction(fixture, first);
                first = await fixture.AsAsync(fixture.OwnerUserId, correction);
                request = correction;
            }
            else if (operation == "review")
            {
                var review = fixture.Review(first, "returned");
                first = await fixture.AsAsync(fixture.ReviewerUserId, review);
                request = review;
                actor = fixture.ReviewerUserId;
            }
        }
        var currentPlan = await fixture.GetPlansAsync();
        var proposed = await fixture.ProposeAsync(currentPlan.Revision,
            owner: new OperatingHolder("member", fixture.BackupMemberId), reviewer: fixture.ApproverMemberId,
            cadence: AdHoc, effectiveFrom: fixture.Today);
        await fixture.AsAsync(fixture.ApproverUserId, new ApproveControlOperatingPlan(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, proposed.Revision, proposed.PlanVersionId,
            "New independent operating assignment."));
        if (operation == "review")
            fixture.Permissions.Managers.Remove(fixture.ReviewerMemberId);
        var before = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOccurrence(fixture.TenantId, fixture.ProgramId, fixture.ControlId, opened.OccurrenceId));

        // Act
        var replayed = await fixture.AsAsync(actor, request);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOccurrence(fixture.TenantId, fixture.ProgramId, fixture.ControlId, opened.OccurrenceId));

        // Assert
        Assert.Equal(Wire(first), Wire(replayed));
        Assert.Equal(before.Revision, retained.Revision);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldReplaySelectedPlanResultGivenUnrelatedHistoricalPlanBodyUnavailable()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await fixture.PlanAsync(cadence: AdHoc);
        var request = fixture.Propose(2, cadence: AdHoc, effectiveFrom: fixture.Today);
        var first = await fixture.AsAsync(fixture.LeadUserId, request);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        var stream = new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId).Stream;
        ControlOperatingPlanProposed? historical = null;
        await foreach (var record in events.ReadAsync(stream, 0, CancellationToken.None))
            if (record.Event is ControlOperatingPlanProposed proposed)
            {
                historical = proposed;
                break;
            }
        var content = new MissingHistoricalBody(services.GetRequiredService<IImmutableContentStore>(), historical!.PlanContent.ContentId);
        var reader = services.GetRequiredService<IAggregateReader>();
        var handler = new ProposeControlOperatingPlanHandler(new NativeOperationExecutor(reader,
            services.GetRequiredService<IAggregateExecutor>(), content), new ControlOperationsReader(reader, events, content),
            reader, services.GetRequiredService<ControlReader>(), services.GetRequiredService<OperatingAuthority>(), content, TimeProvider.System);

        // Act
        var replayed = await handler.HandleAsync(new RequestContext<ProposeControlOperatingPlan>(request,
            ProgramManagementServices.Actor(fixture.LeadUserId)), CancellationToken.None);

        // Assert
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(Wire(first), Wire(replayed.Value!));
        Assert.Equal(0, content.SelectedReads);
        Assert.Equal(3, (await fixture.GetPlansAsync()).Revision);
    }

    static CorrectControlAttestation Correction(OperationsFixture fixture, ControlOccurrenceView current) => new(
        Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId, current.OccurrenceId,
        current.Revision, "complete", DateTimeOffset.UtcNow.AddMinutes(-1), null, null,
        "Complete revised support", null, OperationsFixture.FullSupport, "Corrected the performance record.");

    static string Wire(ControlOccurrenceView view) => JsonSerializer.Serialize(view, ComplianceCoreJsonContext.Default.ControlOccurrenceView);
    static string Wire(ControlOperatingPlanView view) => JsonSerializer.Serialize(view, ComplianceCoreJsonContext.Default.ControlOperatingPlanView);

    static void AssertBounded(Aggregate owner) => Assert.All(owner.GetType().GetFields(
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
        Assert.False(field.GetValue(owner) is ICollection { Count: > 2 },
            $"The operating command owner retains lifetime history in {field.Name}."));

    sealed class MissingHistoricalBody(IImmutableContentStore inner, Uuid selected) : IImmutableContentStore
    {
        public int SelectedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.ContentId == selected)
            {
                SelectedReads++;
                throw new ImmutableContentUnavailableException("An unrelated historical plan body is unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
