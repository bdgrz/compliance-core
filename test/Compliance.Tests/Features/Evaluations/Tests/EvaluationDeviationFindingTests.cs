using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Reactors;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

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
        var trigger = Submitted(owner, reference);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString()));
        var sources = await SourcesAsync(scenario, trigger);

        // Act
        await scenario.RunAsync(new ControlEvaluationDeviationFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests, content, sources));

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
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString()));
        var sources = await SourcesAsync(scenario, Submitted(new ControlEvaluation(TenantId, EvaluationId),
            NativeFacts.Ref(ControlEvaluationContent.SubmissionSchema)));

        // Act
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => scenario.RunAsync(
            new ControlEvaluationDeviationFindingReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests,
                content, sources)));

        // Assert
        Assert.Empty(scenario.SentRequests);
        Assert.NotNull(failure);
    }

    [Fact]
    public async Task ShouldRejectForgedSubmissionGivenAnotherCommittedPayloadWithSameEventIdentity()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var material = Deviation("material");
        var submission = new EvaluationSubmissionView(1, [], "ineffective", [], [material],
            ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), DateTimeOffset.UtcNow);
        var reference = await NativeContent.WriteAsync(content, TenantId, ControlEvaluationContent.SubmissionSchema,
            submission);
        var owner = new ControlEvaluation(TenantId, EvaluationId);
        var actual = Submitted(owner, reference);
        var store = new InMemoryEventStore();
        DomainEventSeed.Attach(actual, owner.Id, 1);
        await store.AppendAsync(owner.Stream, 0, [actual]);
        var forged = actual with { ProgramId = Uuid.CreateVersion4() };
        forged.AttachMetadata(actual.Metadata);
        var scenario = new ReactorScenario(new TenantId(TenantId.ToString())).Given(forged);
        var sources = new EventStoreSourceFactResolver(store);

        // Act
        var error = await Record.ExceptionAsync(() => scenario.RunAsync(new ControlEvaluationDeviationFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests, content, sources)));

        // Assert
        var failure = Assert.IsType<ReactionCommandFailedException>(error);
        Assert.False(failure.Error.IsTransient);
        Assert.Contains("differs", failure.Error.Message, StringComparison.Ordinal);
        Assert.Empty(scenario.SentRequests);
    }

    static async Task<ISourceFactResolver> SourcesAsync(ReactorScenario scenario, ControlEvaluationSubmitted source)
    {
        var store = new InMemoryEventStore();
        DomainEventSeed.Attach(source, EvaluationId, 1);
        await store.AppendAsync(source.Operation.Owner, 0, [source]);
        scenario.Given(source);
        return new EventStoreSourceFactResolver(store);
    }

    static ControlEvaluationSubmitted Submitted(ControlEvaluation owner, Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference) =>
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
