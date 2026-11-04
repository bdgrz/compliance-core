using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Reactors;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateEntryReactionTests
{
    [Fact]
    public async Task ShouldMapEachPlannedEntryToStableCommandAndRetainCheckpointGivenDestinationRetry()
    {
        // Arrange
        await using var source = new MethodologyTemplateScenario(DateTimeOffset.UnixEpoch);
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var template = Uuid.CreateVersion4();
        var owner = new TemplateApplication(tenant, program, template, true);
        var publication = new NativeFactReference(new MethodologyTemplate(template).Stream, Uuid.CreateVersion4(), 3);
        var entries = new ImmutableContentRef(Uuid.CreateVersion4(), ContentDigest.Sha256("[]"u8), 2,
            "application/json", MethodologyTemplateDraft.EntriesSchema);
        Assert.True(owner.ApplyVersion(Uuid.CreateVersion4(), 2, Uuid.CreateVersion4(), publication, 2,
            entries, ["new-control"], ["changed-policy"]).IsSuccess);
        await source.Writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        MethodologyTemplateAppliedV4? applied = null;
        await foreach (var item in source.Events.ReadAsync(owner.Stream, 0))
            applied = item.Event as MethodologyTemplateAppliedV4 ?? applied;
        Assert.NotNull(applied);
        var retry = new RequestError(RequestErrorKind.Conflict, "Destination source is not visible yet.", isTransient: true);
        var scenario = new ReactorScenario(new TenantId(tenant.ToString())).Given(applied)
            .RespondTo<ApplyMethodologyTemplateEntry>(Result.Failure(retry));
        var checkpoints = new InMemoryProjectionCheckpointStore();
        var reactor = new MethodologyTemplateEntryApplicationReactor(checkpoints, scenario.Requests);
        var identity = new CheckpointIdentity(reactor.Name, EventStreamPattern.ForPattern(tenant.ToString(), TemplateApplication.Area));

        // Act
        await Assert.ThrowsAsync<ProcessorFailureException>(() => scenario.RunAsync(reactor));
        var held = await checkpoints.LoadAsync(identity);
        scenario.RespondTo<ApplyMethodologyTemplateEntry>(Result.Success);
        await scenario.RunAsync(reactor);
        var sent = scenario.SentRequests.OfType<ApplyMethodologyTemplateEntry>().ToArray();

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, held);
        Assert.NotEqual(ProjectionCheckpoint.Start, await checkpoints.LoadAsync(identity));
        Assert.Equal(3, sent.Length);
        Assert.Equal(sent[0], sent[1]);
        Assert.Equal("new-control", sent[1].EntryKey);
        Assert.Equal("changed-policy", sent[2].EntryKey);
        Assert.All(sent, command =>
        {
            Assert.Equal(tenant, command.TenantId);
            Assert.Equal(program, command.ProgramId);
            Assert.Equal(template, command.TemplateId);
            Assert.Equal(applied.Metadata.EventId, command.Source.EventId);
            Assert.Equal(Uuid.CreateVersion5(applied.Metadata.EventId, "apply-template-entry:" + command.EntryKey), command.CommandId);
        });
    }

    [Fact]
    public async Task ShouldRefuseBeforeResolvingOrDispatchingGivenPersonalEntryReactionCaller()
    {
        // Arrange
        var request = new ApplyMethodologyTemplateEntry(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            1, "entry", new NativeFactReference(new EventStreamAddress("realm", "area", "resource"), Uuid.CreateVersion4(), 1),
            0, Uuid.CreateVersion4());
        var handler = new ApplyMethodologyTemplateEntryHandler(null!, null!, null!, null!, null!, null!, null!);

        // Act
        var result = await handler.HandleAsync(MethodologyTemplateScenario.Context(request, Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }
}
