using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Contracts;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskReassessmentFanOutResultTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldStopFanOutAndReturnFailureGivenFirstAssessedRiskCommandRefused(bool transient)
    {
        // Arrange
        await using var fixture = new RiskEvaluationSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var assessor = Uuid.CreateVersion4();
        var method = new RiskMethodRules(tenant, program);
        Assert.Null(method.Publish(program, 0, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 12,
            ActorReference.ForMember(assessor, "Lead"), fixture.Clock.Now));
        var riskIds = new List<Uuid>();
        for (var index = 0; index < 2; index++)
        {
            var identifier = $"R-FANOUT-{index}";
            var risk = RiskDraft.IdFor(tenant, program, identifier);
            riskIds.Add(risk);
            var draft = new RiskDraftRules(tenant, risk);
            Assert.True(draft.Create(program, risk, identifier, new RiskDraftContent("Risk", "Scenario", "Effect", null),
                assessor, "Assessor", fixture.Clock.Now).IsSuccess);
            await RiskDraftSeeds.WriteAsync(fixture.Writer, fixture.Content, draft);
            var evaluation = new RiskEvaluationRules(tenant, risk);
            Assert.Null(evaluation.RecordAssessment(program, 0, Uuid.CreateVersion4(), method.Current!, "inherent", 2, 2,
                "Original assessment", assessor, "Assessor", fixture.Clock.Now));
            await fixture.SeedAsync(evaluation);
        }
        Assert.Null(method.Publish(program, 1, RiskMethodTests.Scale(), RiskMethodTests.Scale(), 16,
            ActorReference.ForMember(assessor, "Lead"), fixture.Clock.Now.AddMinutes(1)));
        var published = Assert.IsType<RiskMethodVersionView>(method.Current);
        var methodOwner = await RiskMethodSeeds.OwnerAsync(fixture.Content, method);
        await fixture.Writer.SaveAsync(methodOwner, new RequestDispatchContext(RequestActor.System));
        DomainEventRecord? publication = null;
        await foreach (var record in fixture.Events.ReadAsync(methodOwner.Stream, 0, CancellationToken.None))
            if (record.Event is RiskMethodVersionPublished)
                publication = record;
        var context = new RecordReactorContext(Assert.IsType<DomainEventRecord>(publication));
        var directory = await DirectoryAsync(fixture, tenant);
        var error = new RequestError(RequestErrorKind.Conflict, "The risk command was refused.", isTransient: transient);
        var scenario = new ReactorScenario().RespondTo<RaiseRiskReassessmentTriggers>(Result.Failure(error));
        var consistency = new RiskDraftListReadConsistency(directory, fixture.Events);

        // Act
        var result = await RiskReassessmentFanOut.SendAsync(scenario.Requests, fixture.Reader, directory, consistency,
            context, context.CauseId, tenant, program, RiskStates.MethodChanged, published.MethodVersionId.ToString(),
            published.PublishedAt, published.MethodVersionId, CancellationToken.None);

        // Assert
        var failed = Assert.Single(scenario.SentRequests.OfType<RaiseRiskReassessmentTriggers>());
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Contains(failed.RiskId, riskIds);

        scenario.RespondTo<RaiseRiskReassessmentTriggers>(Result.Success);
        var replay = await RiskReassessmentFanOut.SendAsync(scenario.Requests, fixture.Reader, directory, consistency,
            context, context.CauseId, tenant, program, RiskStates.MethodChanged, published.MethodVersionId.ToString(),
            published.PublishedAt, published.MethodVersionId, CancellationToken.None);
        var retried = scenario.SentRequests.OfType<RaiseRiskReassessmentTriggers>().Skip(1).ToArray();
        Assert.True(replay.IsSuccess);
        Assert.Equal(2, retried.Length);
        Assert.Contains(failed, retried);
        Assert.True(riskIds.ToHashSet().SetEquals(retried.Select(command => command.RiskId)));
    }

    static async Task<FitzRiskDraftDirectory> DirectoryAsync(RiskEvaluationSourceFixture fixture, Uuid tenant)
    {
        var directory = new FitzRiskDraftDirectory(fixture.Kv, fixture.Content,
            new RiskDraftReader(fixture.Reader, fixture.Events, fixture.Content));
        var identity = new CheckpointIdentity(FitzRiskDraftDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area));
        var checkpoint = await directory.LoadCheckpointAsync(identity);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, checkpoint));
        var cursor = EventCursor.Start;
        await foreach (var record in fixture.Events.ReadAsync(identity.Pattern, EventCursor.Start, CancellationToken.None))
        {
            if (record.Event is IRiskDraftFact)
                await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        return directory;
    }
}
