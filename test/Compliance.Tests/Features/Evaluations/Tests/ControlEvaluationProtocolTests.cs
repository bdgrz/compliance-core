using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

/// <summary>
///     The independent evaluation protocol: every evaluation is its own stream, every command is an identified
///     operation, a retest names the exact accepting review of its predecessor and never writes it, and large
///     inspected selections are immutable content.
/// </summary>
public sealed class ControlEvaluationProtocolTests
{
    static readonly IReadOnlyList<EvaluationAssertionConclusion> Ineffective =
    [
        new("design", "effective", "Design meets the objective."),
        new("implementation", "ineffective", "Leavers kept access."),
        new("evidence_sufficiency", "effective", "Evidence is sufficient."),
    ];

    static StartControlEvaluation StartRequest(OperationsFixture fixture, Bdgrz.Compliance.Features.Evaluations.Models.ControlEvaluationPlanVersionView plan,
        Uuid? retestOf = null, Uuid? evaluationId = null) => new(evaluationId ?? Uuid.CreateVersion4(), fixture.TenantId,
        fixture.ProgramId, fixture.ControlId, plan.PlanVersionId, retestOf);

    static async Task<ControlEvaluationView> AcceptedWithMaterialDeviationAsync(OperationsFixture fixture)
    {
        var evaluation = await ControlEvaluationTests.RecordAllAsync(fixture, await ControlEvaluationTests.StartAsync(fixture),
            implementation: "not_met", classification: "material");
        var submitted = await fixture.AsAsync(fixture.OwnerUserId,
            ControlEvaluationTests.Submit(fixture, evaluation, Ineffective));
        return await fixture.AsAsync(fixture.ApproverUserId, ControlEvaluationTests.Review(fixture, submitted, "accepted"));
    }

    static async Task<List<DomainEventRecord>> StreamAsync(OperationsFixture fixture, Uuid evaluationId)
    {
        var records = new List<DomainEventRecord>();
        await foreach (var record in fixture.Provider.GetRequiredService<IEventStore>().ReadAsync(
                           new ControlEvaluation(fixture.TenantId, evaluationId).Stream, 0, CancellationToken.None))
            records.Add(record);
        return records;
    }

    [Fact]
    public async Task ShouldReturnOriginalViewGivenExactStartRetryAfterLaterWork()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        var request = StartRequest(fixture, plan);
        var first = await fixture.AsAsync(fixture.OwnerUserId, request);
        var stepped = await fixture.AsAsync(fixture.OwnerUserId, new RecordControlEvaluationStep(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, first.EvaluationId, first.Steps[0].StepId,
            first.Revision, "met", "Inspected the exact items.", first.Steps[0].InspectedItems));

        // Act
        var retried = await fixture.AsAsync(fixture.OwnerUserId, request);

        // Assert
        Assert.Equal(WireJson.Of(first), WireJson.Of(retried));
        Assert.Equal(1, retried.Revision);
        Assert.Equal(2, stepped.Revision);
        Assert.Equal(2, (await StreamAsync(fixture, first.EvaluationId)).Count);
    }

    [Fact]
    public async Task ShouldConflictGivenSameEvaluationIdWithChangedActorOrIntent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        var request = StartRequest(fixture, plan);
        var first = await fixture.AsAsync(fixture.OwnerUserId, request);

        // Act
        // Assert
        await fixture.Scenario(fixture.OwnerUserId).When(request with { RetestOfEvaluationId = first.EvaluationId })
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.LeadUserId).When(request).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.OwnerUserId).When(request with { PlanVersionId = Uuid.CreateVersion4() })
            .ExpectFailure(RequestErrorKind.Conflict);
        Assert.Single(await StreamAsync(fixture, first.EvaluationId));
    }

    [Fact]
    public async Task ShouldReturnOriginalViewAndConflictOnChangedIntentGivenRetriesOfEveryEvaluationCommand()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var started = await ControlEvaluationTests.StartAsync(fixture);
        var firstStep = new RecordControlEvaluationStep(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, started.EvaluationId, started.Steps[0].StepId, started.Revision, "met",
            "Inspected the exact items.", started.Steps[0].InspectedItems);
        var stepResult = await fixture.AsAsync(fixture.OwnerUserId, firstStep);
        var evaluation = await ControlEvaluationTests.RecordAllAsyncFrom(fixture, stepResult, 1);
        var submit = ControlEvaluationTests.Submit(fixture, evaluation);
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, submit);
        var review = ControlEvaluationTests.Review(fixture, submitted, "changes_requested");
        var reviewed = await fixture.AsAsync(fixture.ApproverUserId, review);

        // Act
        var stepAgain = await fixture.AsAsync(fixture.OwnerUserId, firstStep);
        var submitAgain = await fixture.AsAsync(fixture.OwnerUserId, submit);
        var reviewAgain = await fixture.AsAsync(fixture.ApproverUserId, review);

        // Assert
        Assert.Equal(WireJson.Of(stepResult), WireJson.Of(stepAgain));
        Assert.Equal(WireJson.Of(submitted), WireJson.Of(submitAgain));
        Assert.Equal(WireJson.Of(reviewed), WireJson.Of(reviewAgain));
        Assert.Equal(2, stepAgain.Revision);
        Assert.Equal("submitted", submitAgain.State);
        Assert.Equal("changes_requested", reviewAgain.LatestReview!.Decision);
        await fixture.Scenario(fixture.OwnerUserId).When(firstStep with { Rationale = "A different rationale." })
            .ExpectFailure(RequestErrorKind.Conflict);
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId,
            submit with
            {
                Conclusions = [new("design", "effective", "Changed."), ControlEvaluationTests.AllEffectiveConclusions[1],
                    ControlEvaluationTests.AllEffectiveConclusions[2]],
            },
            RequestErrorKind.Conflict);
        await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId,
            review with { Rationale = "A different rationale." }, RequestErrorKind.Conflict);
        Assert.Equal(6, (await StreamAsync(fixture, started.EvaluationId)).Count);
    }

    [Fact]
    public async Task ShouldRejectReviewGivenRevisionFromBeforeSubmissionAndSubmitGivenStaleRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var recorded = await ControlEvaluationTests.RecordAllAsync(fixture, await ControlEvaluationTests.StartAsync(fixture));

        // Act
        var early = await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId,
            ControlEvaluationTests.Review(fixture, recorded, "accepted"), RequestErrorKind.Conflict);
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Submit(fixture, recorded));
        var stale = await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId,
            ControlEvaluationTests.Submit(fixture, recorded), RequestErrorKind.Conflict);
        var preSubmit = await PersonalControlEvaluationTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId,
            ControlEvaluationTests.Review(fixture, recorded, "accepted"), RequestErrorKind.Conflict);

        // Assert
        Assert.False(early.IsSuccess);
        Assert.False(stale.IsSuccess);
        Assert.False(preSubmit.IsSuccess);
        Assert.Equal("submitted", submitted.State);
        Assert.Equal(recorded.Revision + 1, (await StreamAsync(fixture, recorded.EvaluationId)).Count);
    }

    [Fact]
    public async Task ShouldBindRetestToExactPredecessorReviewAndLeavePredecessorUntouchedGivenRetestStart()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var accepted = await AcceptedWithMaterialDeviationAsync(fixture);
        var predecessorBefore = await StreamAsync(fixture, accepted.EvaluationId);
        var acceptingReview = predecessorBefore.Select(static record => record.Event).OfType<ControlEvaluationReviewed>().Last();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();

        // Act
        var retest = await fixture.AsAsync(fixture.OwnerUserId, StartRequest(fixture, plan, accepted.EvaluationId));
        var predecessorAfter = await StreamAsync(fixture, accepted.EvaluationId);
        var read = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, accepted.EvaluationId));
        var retestFacts = await StreamAsync(fixture, retest.EvaluationId);

        // Assert
        Assert.Equal(predecessorBefore.Count, predecessorAfter.Count);
        Assert.Equal(predecessorBefore.Select(static record => record.Event.Metadata.EventId),
            predecessorAfter.Select(static record => record.Event.Metadata.EventId));
        Assert.Equal(accepted.Revision, read.Revision);
        Assert.Equal("in_progress", read.RetestStatus);
        Assert.Equal([retest.EvaluationId], read.RetestEvaluationIds);
        var started = Assert.IsType<ControlEvaluationStarted>(Assert.Single(retestFacts).Event);
        Assert.Equal(accepted.EvaluationId, started.RetestOfEvaluationId);
        Assert.Equal(new NativeFactReference(new ControlEvaluation(fixture.TenantId, accepted.EvaluationId).Stream,
            acceptingReview.Metadata.EventId, acceptingReview.Metadata.AggregateVersion), started.RetestPredecessor);
        Assert.Equal([Assert.Single(accepted.Deviations).DeviationId], retest.RetestOfDeviationIds);
        Assert.Equal(1, retest.Revision);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenMissingUnacceptedOrOtherControlPredecessor()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        var open = await ControlEvaluationTests.StartAsync(fixture);
        var missing = StartRequest(fixture, plan, Uuid.CreateVersion4());
        var unaccepted = StartRequest(fixture, plan, open.EvaluationId);
        var otherControl = StartRequest(fixture, plan, open.EvaluationId) with { ControlId = Uuid.CreateVersion4() };

        // Act
        // Assert
        await fixture.Scenario(fixture.OwnerUserId).When(missing).ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.OwnerUserId).When(unaccepted).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.LeadUserId).When(otherControl).ExpectFailure(RequestErrorKind.NotFound);
        Assert.Empty(await StreamAsync(fixture, missing.EvaluationId));
        Assert.Empty(await StreamAsync(fixture, unaccepted.EvaluationId));
        Assert.Single(await StreamAsync(fixture, open.EvaluationId));
    }

    [Fact]
    public async Task ShouldFailClosedGivenRetestNamingAnotherFactThanTheAcceptingReview()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var accepted = await AcceptedWithMaterialDeviationAsync(fixture);
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        var predecessorStream = new ControlEvaluation(fixture.TenantId, accepted.EvaluationId).Stream;
        var wrongFact = (await StreamAsync(fixture, accepted.EvaluationId))[0].Event;
        var forged = Uuid.CreateVersion4();
        await EvaluationSeeds.StartAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, fixture.ControlId,
            forged, fixture.ControlVersionId, plan.PlanVersionId, plan.Version, plan.Steps, fixture.OwnerMemberId,
            "Owner", DateTimeOffset.UtcNow, accepted.EvaluationId,
            new NativeFactReference(predecessorStream, wrongFact.Metadata.EventId, wrongFact.Metadata.AggregateVersion),
            [Assert.Single(accepted.Deviations).DeviationId]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<ControlEvaluationReader>();

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.ReadViewAsync(fixture.TenantId, fixture.ProgramId, fixture.ControlId, accepted.EvaluationId));

        // Assert
        Assert.Contains("exact accepting review", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldUpdateDifferentEvaluationsConcurrentlyGivenSameControl()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        var left = await fixture.AsAsync(fixture.OwnerUserId, StartRequest(fixture, plan));
        var right = await fixture.AsAsync(fixture.OwnerUserId, StartRequest(fixture, plan));

        // Act
        var results = await Task.WhenAll(
            fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, left, 0)),
            fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, right, 0)));

        // Assert
        Assert.All(results, static result => Assert.Equal(2, result.Revision));
        Assert.Equal(2, (await StreamAsync(fixture, left.EvaluationId)).Count);
        Assert.Equal(2, (await StreamAsync(fixture, right.EvaluationId)).Count);
        var listed = await fixture.AsAsync(fixture.OutsiderUserId, new ListControlEvaluations(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId));
        Assert.Equal(2, listed.Items.Count);
    }

    [Fact]
    public async Task ShouldKeepEveryInspectedItemGivenFiftyMaximalItemsInOneStepResult()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await ControlEvaluationTests.StartAsync(fixture);
        var items = Enumerable.Range(0, 50).Select(index => new EvaluationInspectedItem("artifact",
            index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + new string('r', 1990),
            new string('v', 200))).ToArray();

        // Act
        var recorded = await fixture.AsAsync(fixture.OwnerUserId, new RecordControlEvaluationStep(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId, evaluation.Steps[0].StepId,
            evaluation.Revision, "met", "Inspected every listed item.", items));
        var stream = await StreamAsync(fixture, evaluation.EvaluationId);
        var read = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId));

        // Assert
        Assert.Equal(50, recorded.Steps[0].Result!.InspectedItems.Count);
        Assert.Equal(items, read.Steps[0].Result!.InspectedItems);
        var fact = Assert.IsType<ControlEvaluationStepRecorded>(stream[^1].Event);
        Assert.True(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fact, fact.GetType(),
            ComplianceCoreJsonContext.Default).Length < 6 * 1024);
    }
}
