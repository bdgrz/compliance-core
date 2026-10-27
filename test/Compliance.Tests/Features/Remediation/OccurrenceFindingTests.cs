using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Queries;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Remediation.Reactors;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class OccurrenceFindingTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly ControlBusinessWindow Window = new ControlCalendarWindow(new DateOnly(2026, 10, 1),
        new DateOnly(2026, 11, 1));
    static readonly Uuid OccurrenceId = ControlOccurrenceIdentity.Of(TenantId, ProgramId, ControlId, Window);
    static readonly IImmutableContentStore Content = new FitzKvImmutableContentStore(
        new PersistenceKvClient(new InMemoryKvClient()));

    [Fact]
    public async Task ShouldRaiseDeterministicFindingGivenFailedAttestation()
    {
        // Arrange
        var attestation = Attestation("failed", "Export tool was down.");
        var attested = await AttestedAsync(attestation, 2);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString()));

        // Act
        await scenario.RunAsync(new ControlOccurrenceFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests, Content, await SourcesAsync(scenario, attested)));

        // Assert
        var request = Assert.IsType<RaiseOccurrenceFinding>(Assert.Single(scenario.SentRequests));
        Assert.Equal(Uuid.CreateVersion5(attestation.AttestationId, "finding"), request.FindingId);
        Assert.Equal("control_occurrence", request.SourceKind);
        Assert.Equal("failed: Export tool was down.", request.SourceText);
        Assert.Equal(attestation.AttestationId.ToString(), request.SourceVersion);
    }

    [Fact]
    public async Task ShouldRaiseSameFindingIdForSameOccurrenceGivenReplayedAttestedFact()
    {
        // Arrange
        var attestation = Attestation("skipped", "Not performed this month.");
        var attested = await AttestedAsync(attestation, 2);
        var first = new ReactorScenario(new TenantId(TenantId.ToString()));
        var replay = new ReactorScenario(new TenantId(TenantId.ToString()));

        // Act
        await first.RunAsync(new ControlOccurrenceFindingReactor(new InMemoryProjectionCheckpointStore(),
            first.Requests, Content, await SourcesAsync(first, attested)));
        await replay.RunAsync(new ControlOccurrenceFindingReactor(new InMemoryProjectionCheckpointStore(),
            replay.Requests, Content, await SourcesAsync(replay, attested)));

        // Assert
        var original = Assert.IsType<RaiseOccurrenceFinding>(Assert.Single(first.SentRequests));
        var repeated = Assert.IsType<RaiseOccurrenceFinding>(Assert.Single(replay.SentRequests));
        Assert.Equal(original, repeated);
        Assert.Equal(Uuid.CreateVersion5(attestation.AttestationId, "finding"), repeated.FindingId);
        Assert.Equal(OccurrenceId, repeated.OccurrenceId);
    }

    [Fact]
    public async Task ShouldIgnoreCompleteAndNotApplicableResultsGivenAttestation()
    {
        // Arrange
        var complete = await AttestedAsync(Attestation("complete", null), 2);
        var notApplicable = await AttestedAsync(Attestation("not_applicable", "No changes shipped."), 3);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString()));

        // Act
        await scenario.RunAsync(new ControlOccurrenceFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests, Content, await SourcesAsync(scenario, complete, notApplicable)));

        // Assert
        Assert.Empty(scenario.SentRequests);
    }

    [Fact]
    public async Task ShouldRaiseOneFindingPerRequestedActionGivenActionRequestedReview()
    {
        // Arrange
        var decisionId = Uuid.CreateVersion4();
        var review = await ControlOperatingContent.WriteAsync(Content, TenantId,
            ControlOperatingContent.ReviewSchema, new ControlOccurrenceReviewView(decisionId,
                Uuid.CreateVersion4(), 1, "action_requested", "Two follow-ups.",
                ["Automate removal.", "Train managers."], Uuid.CreateVersion4(),
                ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer"), DateTimeOffset.UtcNow),
            CancellationToken.None);
        var reviewed = DomainEventSeed.Attach(new ControlOccurrenceReviewed(TenantId, ProgramId, ControlId,
            OccurrenceId, 3, review, Provenance(), review, null), OccurrenceId, 3);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString()));

        // Act
        await scenario.RunAsync(new ControlOccurrenceFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests, Content, await SourcesAsync(scenario, reviewed)));

        // Assert
        var requests = scenario.SentRequests.Cast<RaiseOccurrenceFinding>().ToArray();
        Assert.Equal(["Automate removal.", "Train managers."], requests.Select(r => r.SourceText));
        Assert.All(requests, request => Assert.Equal("occurrence_review", request.SourceKind));
        Assert.Equal(2, requests.Select(request => request.FindingId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldConvertExceptionIdempotentlyGivenSystemReaction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var failed = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed, "failed",
            [], "Export tool was down."));
        var attestationId = failed.Attestations[0].AttestationId;
        var reaction = new RaiseOccurrenceFinding(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion5(attestationId, "finding"), fixture.ControlId,
            failed.OccurrenceId, attestationId.ToString(), "control_occurrence",
            "failed: Export tool was down.");
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Assert
        var finding = await fixture.GetFindingAsync(reaction.FindingId);
        Assert.Equal("control_occurrence", finding.Source.Kind);
        Assert.Equal(failed.OccurrenceId, finding.Source.RecordId);
        Assert.Equal("failed: Export tool was down.", finding.Source.SourceText);
        Assert.Equal(fixture.OwnerMemberId, finding.OwnerMemberId);
        Assert.Equal("system_process", finding.RaisedBy.Kind);
        var findings = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(
            fixture.TenantId, fixture.ProgramId));
        Assert.Single(findings.Items);
        await fixture.Scenario(fixture.LeadUserId).When(reaction)
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldCreateCorrectiveActionGivenManagementRequestedAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        var reviewed = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "action_requested", ["Automate leaver removal."]));
        var decisionId = reviewed.Reviews[0].DecisionId;
        var findingId = Uuid.CreateVersion5(decisionId, "requested-action-0");

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(new RaiseOccurrenceFinding(fixture.TenantId, fixture.ProgramId, findingId,
                fixture.ControlId, reviewed.OccurrenceId, decisionId.ToString(),
                "occurrence_review", "Automate leaver removal."))
            .ExpectSuccess();

        // Assert
        var finding = await fixture.GetFindingAsync(findingId);
        Assert.Equal(decisionId.ToString(), finding.Source.Version);
        var action = Assert.Single(finding.CorrectiveActions);
        Assert.Equal("Automate leaver removal.", action.Description);
        Assert.Equal(fixture.OwnerMemberId, action.OwnerMemberId);
        var work = await fixture.AsAsync(fixture.OwnerUserId, new ListMyControlWork(
            fixture.TenantId, fixture.ProgramId));
        Assert.Contains(work.Items, item => item.Kind == "corrective_action" &&
            item.FindingId == findingId);
    }

    [Fact]
    public async Task ShouldAdmitDefaultActionAtomicallyAndMaterializeItGivenManagementRequestedAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        var reviewed = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "action_requested", ["Automate leaver removal."]));
        var decisionId = reviewed.Reviews[0].DecisionId;
        var findingId = Uuid.CreateVersion5(decisionId, "requested-action-0");
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(new RaiseOccurrenceFinding(fixture.TenantId, fixture.ProgramId, findingId, fixture.ControlId,
                reviewed.OccurrenceId, decisionId.ToString(), "occurrence_review", "Automate leaver removal."))
            .ExpectSuccess();

        // Act
        var stream = await fixture.FindingStreamAsync(findingId);
        var source = await fixture.FindingHistoryAsync(findingId);
        var before = await fixture.GetFindingAsync(findingId);
        await fixture.MaterializeAsync(findingId);
        var after = await fixture.GetFindingAsync(findingId);

        // Assert
        Assert.Equal(2, stream.Revision);
        Assert.Equal(["FindingRaisedV2", "CorrectiveActionAdmitted"], source.Facts.Select(fact => fact.GetType().Name));
        Assert.Equal(FindingRules.RoutedActionId(findingId), Assert.Single(source.Admitted).Operation.LogicalId);
        Assert.Equal(0, Assert.Single(before.CorrectiveActions).Revision);
        Assert.Equal(1, Assert.Single(after.CorrectiveActions).Revision);
        Assert.Equal("system_process", Assert.Single(after.CorrectiveActions).AddedBy.Kind);
    }

    static async Task<ControlOccurrenceAttested> AttestedAsync(ControlAttestationView attestation, long revision)
    {
        var content = await ControlOperatingContent.WriteAsync(Content, TenantId,
            ControlOperatingContent.AttestationSchema, attestation, CancellationToken.None);
        return DomainEventSeed.Attach(new ControlOccurrenceAttested(TenantId, ProgramId, ControlId, OccurrenceId,
            revision, content, Provenance(), content, null), OccurrenceId, (ulong)revision);
    }

    static async Task<ISourceFactResolver> SourcesAsync(ReactorScenario scenario, params DomainEvent[] triggers)
    {
        var owner = new ControlOccurrence(TenantId, ProgramId, ControlId, OccurrenceId);
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Owner");
        var terms = new ControlOccurrenceTerms("recurring", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 5), null,
            new OperatingHolder("member", Uuid.CreateVersion4()), actor, now, Window);
        Assert.True(owner.Materialize(NativeFacts.Root(owner, Uuid.CreateVersion4(),
            nameof(MaterializeControlOccurrence), null), terms, NativeFacts.Ref()).IsSuccess);
        var prefix = new List<DomainEvent>(new AggregateScenario<ControlOccurrence>(owner).PendingEvents);
        if (triggers[0] is ControlOccurrenceReviewed)
            prefix.Add(await AttestedAsync(Attestation("complete", null), 2));
        prefix.AddRange(triggers);
        var store = new InMemoryEventStore();
        await store.AppendAsync(owner.Stream, 0, prefix.ToArray(), CancellationToken.None);
        scenario.Given(prefix.ToArray());
        return new EventStoreSourceFactResolver(store);
    }

    static OperationProvenance Provenance()
    {
        var stream = new ControlOccurrence(TenantId, ProgramId, ControlId, OccurrenceId).Stream;
        var commandId = Uuid.CreateVersion4();
        return new OperationProvenance(commandId, commandId, null, "AttestControlOccurrence",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), stream, "initial", null, new string('a', 64),
            DateTimeOffset.UtcNow, stream, stream);
    }

    static ControlAttestationView Attestation(string result, string? rationale) => new(
        Uuid.CreateVersion4(), 1, result, DateTimeOffset.UtcNow, null, null, null, rationale, [],
        new OperatingHolder("member", Uuid.CreateVersion4()), Uuid.CreateVersion4(),
        ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), DateTimeOffset.UtcNow,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), []);
}
