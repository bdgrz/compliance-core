using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskTreatmentActionNativeTests
{
    [Fact]
    public async Task ShouldRecoverSelectedHistoricalReviewGivenManyLaterRejectedSubmissions()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var at = DateTimeOffset.UtcNow;
        var submitter = RbacIds.Member(fixture.TenantId, fixture.AssessorUserId);
        var reviewer = ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.ApproverUserId), "Approver");
        for (var index = 0; index < 24; index++)
        {
            await RiskSeeds.SubmitActionAsync(provider, fixture.TenantId, fixture.ProgramId, fixture.RiskId,
                added.Value.ActionId, Uuid.CreateVersion4(), $"Submission {index}", [Uuid.CreateVersion4()], submitter,
                ActorReference.ForMember(submitter, "Assessor"), at.AddMinutes(index * 2));
            await RiskSeeds.ReviewActionAsync(provider, fixture.TenantId, fixture.ProgramId, fixture.RiskId,
                added.Value.ActionId, RiskStates.Reject, "Additional evidence needed.", reviewer, at.AddMinutes(index * 2 + 1));
        }
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var current = await fixture.ActionAsync(added.Value.ActionId);
        var facts = await RiskTreatmentActionHistory.ReadAsync(events, current);
        var accepted = facts.OfType<RiskTreatmentActionCompletionReviewAccepted>().First();
        var originalTerminal = facts.OfType<RiskTreatmentActionCompletionReviewed>().First();
        var root = accepted.Operation.RootOperationId;

        // Act
        var selected = await reader.HydrateAsync(new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, added.Value.ActionId, originalTerminal.Operation.LogicalId, root));
        var verified = await services.GetRequiredService<RiskActionReviewVerifier>().VerifyAcceptedAsync(
            NativeFactReference.Of(selected.Stream, accepted), accepted.Intent, root, originalTerminal.Operation.LogicalId,
            CancellationToken.None);
        var view = await services.GetRequiredService<RiskGovernanceReader>().ComposeActionAsync(fixture.TenantId, selected);

        // Assert
        Assert.Null(verified.Error);
        Assert.Equal(73, selected.Revision);
        Assert.Equal(73, facts.Count);
        Assert.Equal(24, view!.Completions.Count);
        Assert.All(view.Completions, completion => Assert.Equal(RiskStates.Reject, completion.ReviewOutcome));
        Assert.Equal(RiskStates.ActionOpen, selected.Status);
        Assert.Equal(accepted, selected.AcceptedReview(root));
        Assert.Equal(originalTerminal, selected.TerminalReview(root));
        Assert.Equal(1, selected.AcceptedReview(root)!.Generation);
        Assert.Equal(view.Completions[0].SubmissionId, selected.SubmissionForReview(root)!.SubmissionId);
        Assert.Equal(NativeAcceptanceDecision.Replayed,
            selected.Acceptance.Decide(AcceptedOperationBinding.Of(originalTerminal.Operation)));
        Assert.Null(current.AcceptedReview(root));
        Assert.Null(current.TerminalReview(root));
        Assert.All(typeof(RiskTreatmentAction).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(selected) is ICollection { Count: > 2 }, field.Name));
    }

    [Theory]
    [InlineData("missing", true)]
    [InlineData("foreign_stream", false)]
    [InlineData("physical_gap", false)]
    [InlineData("changed_body", false)]
    public async Task ShouldClassifyActionHistoryFailureGivenMissingOrContradictoryNativeCut(string fault, bool transient)
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var owner = await fixture.ActionAsync(added.Value.ActionId);
        var events = provider.GetRequiredService<IDomainEventReader>();
        var changed = new AlteredEvents(events, record => fault switch
        {
            "missing" => null,
            "foreign_stream" => record with
            {
                Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(),
                record.Stream.Area, record.Stream.Resource)
            },
            "physical_gap" => record with { ResourceOffset = record.ResourceOffset + 1 },
            "changed_body" => record with { Event = ChangedBody((RiskTreatmentActionAdded)record.Event) },
            _ => record,
        });

        // Act
        var failure = await Record.ExceptionAsync(async () => await RiskTreatmentActionHistory.ReadAsync(changed, owner));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(transient, failure is ImmutableContentUnavailableException);

        static RiskTreatmentActionAdded ChangedBody(RiskTreatmentActionAdded original)
        {
            var changed = original with { RiskId = Uuid.CreateVersion4() };
            changed.AttachMetadata(original.Metadata);
            return changed;
        }
    }

    [Fact]
    public async Task ShouldRejectAnotherActionBodyGivenValidImmutableContentAndNativeReference()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var first = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var second = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var own = await fixture.ActionAsync(first.Value.ActionId);
        var other = await fixture.ActionAsync(second.Value.ActionId);
        var altered = new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId, fixture.RiskId, first.Value.ActionId);
        _ = new AggregateScenario<RiskTreatmentAction>(altered).Given(own.Added! with { ActionContent = other.Added!.ActionContent });
        await using var scope = provider.CreateAsyncScope();

        // Act
        var failure = await Record.ExceptionAsync(async () => await RiskTreatmentActionHistory.ReadInitialAsync(
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(), altered));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Contains("another native identity", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldReadCapturedActionCutGivenLaterCommittedRevision()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0)).ExpectSuccess();
        var captured = await fixture.ActionAsync(added.Value.ActionId);
        await fixture.Scenario(fixture.AssessorUserId).When(new ReviseRiskTreatmentAction(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.RiskId, added.Value.ActionId, 1, "Later content", "Later target",
            "Later evidence", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), worker.MemberId, [])).ExpectSuccess();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var facts = await RiskTreatmentActionHistory.ReadAsync(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), captured);
        var view = await scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>().ComposeActionAsync(fixture.TenantId, captured);
        var current = Assert.Single((await fixture.GovernanceAsync()).TreatmentActions);

        // Assert
        Assert.Single(facts);
        Assert.Equal(1, view!.Revision);
        Assert.NotEqual(current.Title, view.Title);
        Assert.Equal("Later content", current.Title);
    }

    [Fact]
    public async Task ShouldReplayCompletedReceiverBeforeReleasedEvidenceGivenLaterSubmission()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        var first = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 1, [evidence]));
        await fixture.AssignReviewAsync(first.Value.SubmissionId);
        var request = fixture.ReviewAction(added.Value.ActionId, 2, "reject");
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId, request);
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 4, [evidence]));
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IAggregateReader>().HydrateAsync(new RiskTreatmentAction(
            fixture.TenantId, fixture.ProgramId, fixture.RiskId, request.ActionId, selectedReviewRoot: request.CommandId));
        var admitted = owner.AcceptedReview(request.CommandId)!;
        var terminal = Assert.IsType<RiskTreatmentActionCompletionReviewed>(owner.TerminalReview(request.CommandId));
        var unusedProofs = await FindingContent.WriteAsync(services.GetRequiredService<IImmutableContentStore>(),
            fixture.TenantId, RiskContent.ProofsSchema, (IReadOnlyList<EvidenceUseProof>)[], CancellationToken.None);
        var command = new CompleteRiskTreatmentActionReview(terminal.Operation.LogicalId,
            NativeFactReference.Of(owner.Stream, admitted), owner.Stream, request.CommandId, admitted.Generation, null,
            admitted.Intent, request.ActionId, unusedProofs);
        var before = owner.CommittedStreamPosition;

        // Act
        var result = await services.GetRequiredService<IRequestBus>().DispatchAsync(command,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var after = await fixture.ActionAsync(request.ActionId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(NativeFactReference.Of(owner.Stream, terminal), Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        Assert.Equal(before, after.CommittedStreamPosition);
        Assert.Equal(RiskStates.ActionSubmitted, after.Status);
        Assert.False((await services.GetRequiredService<IAggregateReader>().HydrateAsync(new EvidenceRequest(
            fixture.TenantId, fixture.ProgramId, evidence))).Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldKeepNativeActionCutGivenIndependentAuditSession()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var request = fixture.AddAction(worker, [], 0);
        var auditId = Uuid.CreateVersion4();
        var auditStream = new EventStreamAddress(fixture.TenantId.ToString(), "audit-sessions", auditId.ToString());
        DomainEvent audit = new ActionHistoryAudit();
        audit.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), auditId, 0, DateTimeOffset.UtcNow, IsAudit: true));
        await provider.GetRequiredService<IEventStore>().AppendAsync(auditStream, 0, [audit]);
        await fixture.Scenario(fixture.AssessorUserId).When(request).ExpectSuccess();
        var owner = await fixture.ActionAsync(request.ActionId);

        // Act
        var facts = await RiskTreatmentActionHistory.ReadAsync(provider.GetRequiredService<IDomainEventReader>(), owner);

        // Assert
        Assert.Equal(1UL, owner.CommittedStreamPosition);
        Assert.Equal(1UL, owner.Version);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(owner.Added!.Metadata.EventId, Assert.Single(facts).Metadata.EventId);
    }

    sealed record ActionHistoryAudit : DomainEvent;

    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord?> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                if (alter(record) is { } selected)
                    yield return selected;
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                if (alter(record) is { } selected)
                    yield return selected;
        }
    }
}
