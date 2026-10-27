using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftNativeIntegrityTests
{
    [Theory]
    [InlineData("first_owner")]
    [InlineData("affected_owner")]
    [InlineData("expected_owner")]
    [InlineData("command")]
    [InlineData("gate")]
    [InlineData("actor")]
    [InlineData("aggregate")]
    [InlineData("native_payload")]
    public async Task ShouldRollBackBothRiskDraftProjectionsGivenMalformedNativeBinding(string malformed)
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-BOUND");
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        Assert.True((await new CreateRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
            new CreateRiskDraft(id, tenant, program, "R-BOUND", "Title", "Scenario", "Effect"), user), CancellationToken.None)).IsSuccess);
        Assert.True((await new ReviseRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
            new ReviseRiskDraft(Uuid.CreateVersion4(), tenant, program, id, 1, "Revision", "Scenario", "Effect"), user), CancellationToken.None)).IsSuccess);
        var facts = await fixture.FactsAsync(tenant, id);
        var original = Assert.IsType<RiskDraftRevised>(facts[1]);
        var other = new EventStreamAddress(Uuid.CreateVersion4().ToString(), RiskDraft.Area, id.ToString());
        var operation = malformed switch
        {
            "first_owner" => original.Operation with { FirstOwner = other },
            "affected_owner" => original.Operation with { AffectedOwner = other },
            "expected_owner" => original.Operation with { ExpectedRevisionOwner = other },
            "command" => original.Operation with { CommandKind = nameof(CreateRiskDraft) },
            "gate" => original.Operation with { OriginalExpectedRevision = 0 },
            "actor" => original.Operation with { Actor = original.Operation.Actor with { Display = "Another author" } },
            _ => original.Operation,
        };
        var body = (await fixture.Drafts.ReadAsync(tenant, program, id)).Current!;
        var reference = malformed == "native_payload" ? await NativeContent.WriteAsync(fixture.Content, tenant, RiskDraftFactContent.Schema,
            body with { Content = body.Content with { Title = "Forged native payload" } }) : original.Content;
        var invalid = original with { Operation = operation, Content = reference };
        invalid.AttachMetadata(original.Metadata with { AggregateId = malformed == "aggregate" ? Uuid.CreateVersion4() : id });
        var current = new FitzRiskDraftDirectory(new InMemoryKvClient(), fixture.Content, fixture.Drafts);
        var history = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient(), fixture.Content, fixture.Drafts);
        var currentIdentity = Identity(FitzRiskDraftDirectory.ProjectorName, tenant);
        var historyIdentity = Identity(FitzRiskDraftHistoryDirectoryV1.ProjectorName, tenant);

        // Act
        await using (var batch = await current.BeginAsync(new ProjectionBatchContext(currentIdentity, ProjectionCheckpoint.Start)))
        {
            await current.ApplyAsync(facts[0]);
            await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () => await current.ApplyAsync(invalid));
        }
        await using (var batch = await history.BeginAsync(new ProjectionBatchContext(historyIdentity, ProjectionCheckpoint.Start)))
        {
            await history.ApplyAsync(facts[0]);
            await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () => await history.ApplyAsync(invalid));
        }

        // Assert
        Assert.Null(await current.GetAsync(tenant, id));
        Assert.Null(await current.GetRevisionAsync(tenant, id, 1));
        Assert.Null(await history.GetRevisionAsync(tenant, id, 1));
        Assert.Equal(ProjectionCheckpoint.Start, await current.LoadCheckpointAsync(currentIdentity));
        Assert.Equal(ProjectionCheckpoint.Start, await history.LoadCheckpointAsync(historyIdentity));
    }

    [Fact]
    public async Task ShouldRejectAlienEarlierRiskDraftRevisionGivenLegitimateFinalOwnerCut()
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-LOCAL");
        await fixture.SeedProgramAsync(tenant, program, RbacIds.Member(tenant, user));
        Assert.True((await new CreateRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
            new CreateRiskDraft(id, tenant, program, "R-LOCAL", "Local", "Scenario", "Effect"), user), CancellationToken.None)).IsSuccess);
        Assert.True((await new ReviseRiskDraftHandler(fixture.Operations()).HandleAsync(RiskDraftSourceFixture.Context(
            new ReviseRiskDraft(Uuid.CreateVersion4(), tenant, program, id, 1, "Final local", "Scenario", "Effect"), user), CancellationToken.None)).IsSuccess);
        var alienTenant = Uuid.CreateVersion4();
        var alienProgram = Uuid.CreateVersion4();
        var alien = new RiskDraftRules(alienTenant, RiskDraft.IdFor(alienTenant, alienProgram, "R-ALIEN"));
        Assert.True(alien.Create(alienProgram, alien.Id, "R-ALIEN", new RiskDraftContent("Alien", "Alien scenario", "Effect", null),
            Uuid.CreateVersion4(), "Alien author", DateTimeOffset.UtcNow).IsSuccess);
        var replacement = Assert.Single(await fixture.PublishAsync(alien));
        var events = new ChangedHistory(fixture.Events, id, replacement);
        var reader = new RiskDraftReader(fixture.Reader, events, fixture.Content);

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () =>
            await reader.ReadRevisionsAsync(tenant, program, id));

        // Assert
        Assert.Contains("owner", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, (await fixture.Drafts.ReadRevisionsAsync(tenant, program, id)).Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRejectMalformedNullNarrativeGivenVerifiedRiskDraftBlob(bool missingIdentifier)
    {
        // Arrange
        await using var fixture = new RiskDraftSourceFixture();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var rules = new RiskDraftRules(tenant, RiskDraft.IdFor(tenant, program, "R-NULL"));
        Assert.True(rules.Create(program, rules.Id, "R-NULL", new RiskDraftContent("Title", "Scenario", "Effect", null),
            Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        var original = Assert.IsType<RiskDraftCreated>(Assert.Single(await fixture.PublishAsync(rules)));
        var malformed = missingIdentifier ? rules.Current! with { Identifier = null! } : rules.Current! with { Content = null! };
        var reference = await NativeContent.WriteAsync(fixture.Content, tenant, RiskDraftFactContent.Schema, malformed);
        var invalid = original with { Header = missingIdentifier ? original.Header with { Identifier = null! } : original.Header, Content = reference };

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(async () =>
            await RiskDraftFactContent.ReadAsync(fixture.Content, invalid));

        // Assert
        Assert.Contains("risk draft", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    static CheckpointIdentity Identity(string projector, Uuid tenant) => new(projector,
        EventStreamPattern.ForPattern(tenant.ToString(), RiskDraft.Area));

    sealed class ChangedHistory(IDomainEventReader inner, Uuid local, DomainEvent replacement) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                yield return Change(record);
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                yield return Change(record);
        }
        DomainEventRecord Change(DomainEventRecord record) => record.Event is RiskDraftCreated { RiskId: var risk } && risk == local
            ? record with { Event = replacement } : record;
    }
}
