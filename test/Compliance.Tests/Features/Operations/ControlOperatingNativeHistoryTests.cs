using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class ControlOperatingNativeHistoryTests
{
    static readonly ControlCadence AdHoc = new("ad_hoc", DueWithinDays: 10);

    [Theory]
    [InlineData("plan", "missing", true)]
    [InlineData("plan", "foreign_stream", false)]
    [InlineData("plan", "physical_gap", false)]
    [InlineData("plan", "changed_latest", false)]
    [InlineData("occurrence", "missing", true)]
    [InlineData("occurrence", "foreign_stream", false)]
    [InlineData("occurrence", "physical_gap", false)]
    [InlineData("occurrence", "changed_latest", false)]
    public async Task ShouldClassifyExactHistoryFailureGivenMissingOrContradictoryOperatingCut(string kind, string fault, bool transient)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await fixture.PlanAsync(cadence: AdHoc);
        var opened = await fixture.AsAsync(fixture.OwnerUserId, fixture.Open("Release performance", fixture.Today));
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(opened));
        var source = provider.GetRequiredService<IDomainEventReader>();
        var changed = new AlteredEvents(source, record => fault switch
        {
            "missing" => null,
            "foreign_stream" => record with { Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(), record.Stream.Area, record.Stream.Resource) },
            "physical_gap" => record with { ResourceOffset = record.ResourceOffset + 1 },
            "changed_latest" => record with { Event = ChangedLatest(record.Event) },
            _ => record,
        });
        var plan = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        var occurrence = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOccurrence(fixture.TenantId, fixture.ProgramId, fixture.ControlId, opened.OccurrenceId));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (kind == "plan")
                await ControlOperatingPlanHistory.ReadAsync(changed, plan);
            else
                await ControlOccurrenceHistory.ReadAsync(changed, occurrence);
        });

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(transient, failure is ImmutableContentUnavailableException);
    }

    [Fact]
    public async Task ShouldReconstructOnlyCapturedPlanCutGivenLaterApprovedSuccessor()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var initial = await fixture.PlanAsync(cadence: AdHoc);
        var captured = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        await fixture.PlanAsync(cadence: AdHoc, effectiveFrom: fixture.Today);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var line = await ControlOperatingPlanLine.ReadAsync(captured,
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), fixture.Content, fixture.TenantId, CancellationToken.None);
        var current = await fixture.GetPlansAsync();

        // Assert
        Assert.Equal(2, line.Revision);
        Assert.Single(line.Plans);
        Assert.Equal(initial.PlanVersionId, line.Current!.PlanVersionId);
        Assert.Equal("approved", line.Current.Status);
        Assert.Equal(4, current.Revision);
        Assert.NotEqual(line.Current.PlanVersionId, current.Current!.PlanVersionId);
    }

    [Fact]
    public async Task ShouldPreserveCapturedOccurrenceBodyGivenLaterReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await fixture.PlanAsync(cadence: AdHoc);
        var opened = await fixture.AsAsync(fixture.OwnerUserId, fixture.Open("Release performance", fixture.Today));
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(opened));
        var captured = await ProgramManagementServices.HydrateAsync(provider,
            new ControlOccurrence(fixture.TenantId, fixture.ProgramId, fixture.ControlId, opened.OccurrenceId));
        await fixture.AsAsync(fixture.ReviewerUserId, fixture.Review(attested, "returned"));

        // Act
        var view = await ControlOccurrenceFold.ReadAsync(captured, provider.GetRequiredService<IDomainEventReader>(),
            fixture.Content, fixture.TenantId, CancellationToken.None);
        var current = await fixture.GetOccurrenceAsync(opened.OccurrenceId);

        // Assert
        Assert.Equal(2, view!.Revision);
        Assert.Single(view.Attestations);
        Assert.Empty(view.Reviews);
        Assert.Equal("submitted", view.State);
        Assert.Equal("returned", current.State);
        Assert.Single(current.Reviews);
    }

    [Fact]
    public async Task ShouldRefuseAnotherOwnerPlanBodyGivenVerifiedImmutableReferenceAndNativeFact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        await fixture.ProposeAsync(0, cadence: AdHoc);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var original = await reader.HydrateAsync(new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        var source = Assert.IsType<ControlOperatingPlanProposed>(original.Latest);
        var otherControl = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var target = new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, otherControl, command);
        var actor = new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, source.Operation.Actor.Display);
        var operation = ControlOperationsSource.Provenance(command, nameof(ProposeControlOperatingPlan), target.Stream,
            actor, 0, new string('a', 64), source.Operation.AcceptedAt);
        Assert.True(target.Propose(operation, source.PlanContent, source.FirstResponse!, source.OperationResult!).IsSuccess);
        await services.GetRequiredService<IAggregateWriter>().SaveAsync(target, new RequestDispatchContext(RequestActor.System));
        var operating = services.GetRequiredService<ControlOperationsReader>();

        // Act
        var failure = await Record.ExceptionAsync(async () => await operating.ReadLineAsync(fixture.TenantId,
            fixture.ProgramId, otherControl, command));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Contains("native source scope", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldExcludeIndependentAuditSessionsGivenPlanAndOccurrenceNativeHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var plan = new ControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId);
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var planAudit = new OperatingAuditEmitter(plan.Id, plan.Stream);
        planAudit.Record();
        await writer.SaveAsync(planAudit, new RequestDispatchContext(RequestActor.System));
        await fixture.PlanAsync(cadence: AdHoc);
        var request = fixture.Open("Release performance", fixture.Today);
        var occurrence = new ControlOccurrence(fixture.TenantId, fixture.ProgramId, fixture.ControlId, request.BusinessWindow);
        var occurrenceAudit = new OperatingAuditEmitter(occurrence.Id, occurrence.Stream);
        occurrenceAudit.Record();
        await writer.SaveAsync(occurrenceAudit, new RequestDispatchContext(RequestActor.System));
        await fixture.AsAsync(fixture.OwnerUserId, request);
        var capturedPlan = await ProgramManagementServices.HydrateAsync(provider, plan);
        var capturedOccurrence = await ProgramManagementServices.HydrateAsync(provider, occurrence);
        var events = provider.GetRequiredService<IDomainEventReader>();

        // Act
        var planFacts = await ControlOperatingPlanHistory.ReadAsync(events, capturedPlan);
        var occurrenceFacts = await ControlOccurrenceHistory.ReadAsync(events, capturedOccurrence);
        var audits = new List<DomainEventRecord>();
        foreach (var area in new[] { ControlOperatingPlan.Area, ControlOccurrence.Area })
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(fixture.TenantId.ToString(), area),
                               EventCursor.Start, CancellationToken.None))
                if (record.Event.Metadata.IsAudit)
                    audits.Add(record);

        // Assert
        Assert.Equal(2UL, capturedPlan.CommittedStreamPosition);
        Assert.Equal(2UL, capturedPlan.Version);
        Assert.Equal(2, planFacts.Count);
        Assert.Equal(1UL, capturedOccurrence.CommittedStreamPosition);
        Assert.Equal(1UL, capturedOccurrence.Version);
        Assert.Single(occurrenceFacts);
        Assert.Contains(audits, record => record.Event.Metadata.AggregateId == plan.Id && record.Stream != plan.Stream);
        Assert.Contains(audits, record => record.Event.Metadata.AggregateId == occurrence.Id && record.Stream != occurrence.Stream);
    }

    static DomainEvent ChangedLatest(DomainEvent original)
    {
        var changed = original switch
        {
            ControlOperatingPlanApproved approved => approved with { Rationale = "Another original approval" },
            ControlOccurrenceAttested attested => attested with { AttestationContent = attested.AttestationContent with { Sha256 = new string('f', 64) } },
            _ => original,
        };
        if (!ReferenceEquals(changed, original))
            changed.AttachMetadata(original.Metadata);
        return changed;
    }

    sealed record OperatingHistoryAudit : DomainEvent;

    sealed class OperatingAuditEmitter(Uuid id, EventStreamAddress stream) : Aggregate(id, stream)
    {
        public void Record() => AuditEvent(new OperatingHistoryAudit());
    }

    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } changed)
                    yield return changed;
        }
    }
}
