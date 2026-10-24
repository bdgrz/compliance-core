using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class EvaluationDeviationFindingTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid EvaluationId = Uuid.CreateVersion4();

    static EvaluationDeviationView Deviation(string classification) => new(
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, classification, "Leavers kept access.",
        classification == "material" ? "pending_routing" : "dispositioned", null, null, null, null,
        ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), DateTimeOffset.UtcNow);

    [Fact]
    public async Task ShouldRouteOnlyMaterialDeviationsGivenSubmissionFact()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var material = Deviation("material");
        var submission = new EvaluationSubmissionView(1, [], "ineffective", [],
            [material, Deviation("minor")], ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"),
            DateTimeOffset.UtcNow);
        var reference = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.SubmissionSchema,
            submission);
        var owner = new ControlEvaluation(TenantId, EvaluationId);
        var scenario = new ReactorScenario().Given(Submitted(owner, reference));

        // Act
        await scenario.RunAsync(new ControlEvaluationDeviationFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests, content));

        // Assert
        var request = Assert.IsType<RaiseEvaluationDeviationFinding>(
            Assert.Single(scenario.SentRequests));
        Assert.Equal(material.DeviationId, request.DeviationId);
        Assert.Equal(Uuid.CreateVersion5(material.DeviationId, "finding"), request.FindingId);
        Assert.Equal(EvaluationId, request.EvaluationId);
    }

    [Fact]
    public async Task ShouldRetryTransientlyGivenSubmissionContentThatCannotBeVerified()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var scenario = new ReactorScenario().Given(Submitted(new ControlEvaluation(TenantId, EvaluationId),
            NativeFacts.Ref(ControlEvaluationContent.SubmissionSchema)));

        // Act
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => scenario.RunAsync(
            new ControlEvaluationDeviationFindingReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
                content)));

        // Assert
        Assert.Empty(scenario.SentRequests);
        Assert.NotNull(failure);
    }

    static ControlEvaluationSubmitted Submitted(ControlEvaluation owner, Bdgrz.Compliance.Features.Proofs.ImmutableContentRef reference) =>
        new(TenantId, ProgramId, ControlId, EvaluationId, 5, reference,
            NativeFacts.Root(owner, Uuid.CreateVersion4(), "SubmitControlEvaluation", 4), NativeFacts.Ref(),
            NativeFacts.Ref());

    [Fact]
    public async Task ShouldRaiseOwnedFindingWithCorrectiveActionIdempotentlyGivenSystemReaction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync(
            [new("implementation", "reperformance", [new("artifact", "exports/q3.csv", "sha256:1")],
                "Every leaver was removed.")]);
        var evaluation = await fixture.AsAsync(fixture.OwnerUserId, new StartControlEvaluation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, plan.PlanVersionId));
        evaluation = await fixture.AsAsync(fixture.OwnerUserId, new RecordControlEvaluationStep(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId,
            evaluation.Steps[0].StepId, evaluation.Revision, "not_met", "Two leavers active.",
            evaluation.Steps[0].InspectedItems, "material", "Two leavers kept access."));
        evaluation = await fixture.AsAsync(fixture.OwnerUserId, new SubmitControlEvaluation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId,
            evaluation.Revision,
            [new("design", "not_tested", "Out of scope."),
                new("implementation", "ineffective", "Leavers kept access."),
                new("evidence_sufficiency", "not_tested", "Out of scope.")]));
        var deviation = Assert.Single(evaluation.Deviations);
        var reaction = new RaiseEvaluationDeviationFinding(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion5(deviation.DeviationId, "finding"), fixture.ControlId,
            evaluation.EvaluationId, deviation.DeviationId);
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Assert
        var finding = await fixture.GetFindingAsync(reaction.FindingId);
        Assert.Equal("evaluation_deviation", finding.Source.Kind);
        Assert.Equal(deviation.DeviationId, finding.Source.RecordId);
        Assert.Equal("Two leavers kept access.", finding.Source.SourceText);
        Assert.Equal(fixture.OwnerMemberId, finding.OwnerMemberId);
        Assert.Single(finding.CorrectiveActions);
        Assert.Contains(new FindingLink("control_evaluation", evaluation.EvaluationId.ToString()),
            finding.Links);
        var findings = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(
            fixture.TenantId, fixture.ProgramId));
        Assert.Single(findings.Items);
        var read = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId));
        Assert.Equal("routed_to_finding", Assert.Single(read.Deviations).Status);
        await fixture.Scenario(fixture.LeadUserId).When(reaction)
            .ExpectFailure(RequestErrorKind.Forbidden);
    }
}
