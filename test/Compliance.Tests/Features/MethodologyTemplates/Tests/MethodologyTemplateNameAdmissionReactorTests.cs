using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateNameAdmissionReactorTests
{
    [Fact]
    public async Task ShouldHoldCheckpointAndRepeatSameCreationCommandGivenAdmissionEffectFailsThenReplays()
    {
        // Arrange
        var claimed = Claim();
        var failure = new RequestError(RequestErrorKind.Conflict, "The exact creation source is pending.", true);
        var scenario = new ReactorScenario().Given(claimed).RespondTo<CreateMethodologyTemplateLine>(Result.Failure(failure));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new MethodologyTemplateNameAdmissionReactor(checkpoints, scenario.Requests);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);

        // Act
        var rejected = await Assert.ThrowsAsync<ProcessorFailureException>(() => scenario.RunAsync(reactor));
        var held = await checkpoints.LoadAsync(identity);
        scenario.RespondTo<CreateMethodologyTemplateLine>(Result.Success);
        await scenario.RunAsync(reactor);
        var done = await checkpoints.LoadAsync(identity);
        var commands = scenario.SentRequests.OfType<CreateMethodologyTemplateLine>().ToArray();

        // Assert
        Assert.Same(failure, rejected.Error);
        Assert.Equal(ProjectionCheckpoint.Start, held);
        Assert.NotEqual(ProjectionCheckpoint.Start, done);
        Assert.Equal(2, commands.Length);
        Assert.Equal(commands[0], commands[1]);
        Assert.Equal(claimed.Owner.Resource, commands[0].TemplateId.ToString());
        Assert.Equal(Uuid.CreateVersion5(claimed.Metadata.EventId, nameof(CreateMethodologyTemplateLine)), commands[0].CommandId);
    }

    [Fact]
    public async Task ShouldRefuseWithoutSendingGivenClaimTargetsNonPlatformLine()
    {
        // Arrange
        var original = Claim();
        var foreign = new EventStreamAddress(Uuid.CreateVersion4().ToString(), MethodologyTemplate.Area, Uuid.CreateVersion4().ToString());
        var claimed = original with { Owner = foreign };
        claimed.AttachMetadata(original.Metadata);
        var scenario = new ReactorScenario().Given(claimed);
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new MethodologyTemplateNameAdmissionReactor(checkpoints, scenario.Requests);
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);

        // Act
        var rejected = await Assert.ThrowsAsync<ProcessorFailureException>(() => scenario.RunAsync(reactor));
        var held = await checkpoints.LoadAsync(identity);

        // Assert
        Assert.IsType<InvalidOperationException>(rejected.InnerException);
        Assert.Empty(scenario.SentRequests);
        Assert.Equal(ProjectionCheckpoint.Start, held);
    }

    static MethodologyTemplateNameClaimed Claim()
    {
        var owner = new MethodologyTemplateNameClaim("Firm methodology");
        var target = new MethodologyTemplate(Uuid.CreateVersion4()).Stream;
        var command = Uuid.CreateVersion4();
        var creator = Uuid.CreateVersion4();
        var intent = new ImmutableContentRef(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json", MethodologyTemplateNameClaim.CreationSchema);
        var operation = new OperationProvenance(command, command, null, nameof(CreateMethodologyTemplateV3),
            ActorReference.ForPlatformOperator(creator, creator.ToString()), owner.Stream, target.Resource, null,
            intent.Sha256, DateTimeOffset.UnixEpoch, target, target);
        var claimed = new MethodologyTemplateNameClaimed(target, 0, 1, intent, operation,
            new ImmutableContentRef(Uuid.CreateVersion4(), new string('b', 64), 2, "application/json", FirstResponses.Schema));
        claimed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), owner.Id, 1, DateTimeOffset.UnixEpoch));
        return claimed;
    }
}
