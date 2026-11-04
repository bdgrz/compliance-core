using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateDraftConclusionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldHoldCheckpointAndRepeatSameCommandGivenPublicationOrClosureRefusedThenReplayed(bool discard)
    {
        // Arrange
        var template = Uuid.CreateVersion4();
        var draft = Uuid.CreateVersion4();
        DomainEvent fact = discard
            ? new MethodologyTemplateDraftDiscarded(template, draft, 1, Uuid.CreateVersion4(), "Superseded")
            : new MethodologyTemplateDraftApproved(template, draft, 1,
                new ImmutableContentRef(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json",
                    MethodologyTemplateDraft.EntriesSchema), Uuid.CreateVersion4());
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), MethodologyTemplateDraft.Identity(template, draft),
            4, DateTimeOffset.UnixEpoch));
        var failure = new RequestError(RequestErrorKind.Conflict, "Line operation is pending.", true);
        var scenario = new ReactorScenario().Given(fact);
        scenario.RespondTo<PublishMethodologyTemplateVersion>(Result.Failure(failure));
        scenario.RespondTo<CloseMethodologyTemplateDraft>(Result.Failure(failure));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new MethodologyTemplateDraftConclusionReactor(checkpoints, scenario.Requests);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);

        // Act
        var rejected = await Assert.ThrowsAsync<ProcessorFailureException>(() => scenario.RunAsync(reactor));
        var held = await checkpoints.LoadAsync(identity);
        scenario.RespondTo<PublishMethodologyTemplateVersion>(Result.Success);
        scenario.RespondTo<CloseMethodologyTemplateDraft>(Result.Success);
        await scenario.RunAsync(reactor);
        var completed = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.Same(failure, rejected.Error);
        Assert.Equal(ProjectionCheckpoint.Start, held);
        Assert.NotEqual(ProjectionCheckpoint.Start, completed);
        if (discard)
        {
            var sent = scenario.SentRequests.OfType<CloseMethodologyTemplateDraft>().ToArray();
            Assert.Equal(2, sent.Length);
            Assert.Equal(sent[0], sent[1]);
            Assert.Equal(Uuid.CreateVersion5(fact.Metadata.EventId, nameof(CloseMethodologyTemplateDraft)), sent[0].CommandId);
            Assert.Equal(template, sent[0].TemplateId);
            Assert.Equal(draft, sent[0].DraftId);
            Assert.Empty(scenario.SentRequests.OfType<PublishMethodologyTemplateVersion>());
        }
        else
        {
            var sent = scenario.SentRequests.OfType<PublishMethodologyTemplateVersion>().ToArray();
            Assert.Equal(2, sent.Length);
            Assert.Equal(sent[0], sent[1]);
            Assert.Equal(Uuid.CreateVersion5(fact.Metadata.EventId, nameof(PublishMethodologyTemplateVersion)), sent[0].CommandId);
            Assert.Equal(template, sent[0].TemplateId);
            Assert.Equal(draft, sent[0].DraftId);
            Assert.Empty(scenario.SentRequests.OfType<CloseMethodologyTemplateDraft>());
        }
    }
}
