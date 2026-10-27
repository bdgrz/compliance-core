using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Handlers;
using Bdgrz.Compliance.Features.Work.Readers;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WeeklyWorkDigestReceiverRecoveryTests
{
    [Fact]
    public async Task ShouldRecoverOneAdmissionGivenDeliveryAppendCommittedBeforeUnknownResponse()
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(fixture.TenantId, user);
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId, member, digest, source,
            1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        var lost = new CommitThenFailOnceExecutor(fixture.Executor);
        var operations = new WeeklyDigestOperations(new NativeOperationExecutor(fixture.Reader, lost, fixture.Content),
            fixture.Events, fixture.Sources, fixture.Content, new WeeklyWorkDigestRecipientResolver(fixture.Reader, new UnusedAddresses()), fixture.Clock);
        // A recipient resolver with no addresses would suppress a fresh request; use a current eligible recipient via normal handler first.
        // The injected lost response applies to an exact new NoChange receipt, whose existing attempt skips fresh recipients.
        Assert.True((await fixture.DeliveryHandler().HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        var noChange = request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2 };

        // Act
        var recovered = await new DeliverWeeklyWorkDigestHandler(operations).HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(noChange, source), CancellationToken.None);
        var replay = await fixture.DeliveryHandler().HandleAsync(
            WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(noChange, source), CancellationToken.None);
        var history = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);

        // Assert
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(3, history.View.Revision);
        Assert.Single(history.View.DeliveryAttempts);
        Assert.Single(history.Facts.OfType<WeeklyWorkDigestDeliveryRequested>(), fact => fact.NoChange && fact.Operation.LogicalId == noChange.CommandId);
    }

    [Theory]
    [InlineData("physical")]
    [InlineData("realm")]
    [InlineData("global")]
    [InlineData("source")]
    [InlineData("actor")]
    [InlineData("intent")]
    [InlineData("no_change")]
    [InlineData("result")]
    public async Task ShouldRefuseContradictoryNativeReceiverGivenExactHydratedHistory(string fault)
    {
        // Arrange
        await using var fixture = new WeeklyWorkDigestCommandHandlerTests.Fixture();
        var user = Uuid.CreateVersion4();
        var digest = await fixture.ScheduleOneAsync(user);
        var source = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digest);
        var request = new DeliverWeeklyWorkDigest(Uuid.CreateVersion4(), fixture.TenantId,
            RbacIds.Member(fixture.TenantId, user), digest, source, 1, 1, WeeklyWorkDigest.DeliveryKeyFor(digest, 1));
        Assert.True((await fixture.DeliveryHandler().HandleAsync(WeeklyWorkDigestCommandHandlerTests.Fixture.DeliveryContext(request, source), CancellationToken.None)).IsSuccess);
        var before = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events, fixture.Sources).ReadWeeklyAsync(fixture.TenantId, digest);
        var fact = Assert.Single(before.Facts.OfType<WeeklyWorkDigestDeliveryRequested>());
        var changed = fault switch
        {
            "physical" => fact with { SourceProof = fact.SourceProof with { PhysicalPosition = fact.SourceProof.PhysicalPosition + 1 } },
            "realm" => fact with { SourceProof = fact.SourceProof with { RealmPosition = fact.SourceProof.RealmPosition + 1 } },
            "global" => fact with { SourceProof = fact.SourceProof with { GlobalPosition = fact.SourceProof.GlobalPosition + 1 } },
            "source" => fact with { SourceProof = fact.SourceProof with { Source = new EventStreamAddress(fixture.TenantId.ToString(), WeeklyWorkDigest.Area, Uuid.CreateVersion4().ToString()) } },
            "actor" => fact with { OriginalActor = fact.OriginalActor with { Subject = "another-process" } },
            "intent" => fact with { Authored = fact.Authored with { ExpectedRevision = 999 } },
            "no_change" => fact with { NoChange = true },
            "result" => fact with { OperationResult = fact.ResponseContent },
            _ => throw new InvalidOperationException()
        };
        changed.AttachMetadata(fact.Metadata);
        var raw = new AlteredEvents(fixture.Events, record => record.Event.Metadata.EventId == fact.Metadata.EventId ? record with { Event = changed } : record);
        var owner = await fixture.Reader.HydrateAsync(new WeeklyWorkDigest(fixture.TenantId, digest));

        // Act
        var failure = await Record.ExceptionAsync(async () => await WeeklyWorkDigestHistory.ReadAsync(raw, fixture.Sources, owner));

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.IsNotType<ImmutableContentUnavailableException>(failure);
    }

    sealed class UnusedAddresses : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default) => ValueTask.FromResult<EmailAddressView?>(null);
        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<EmailAddressView>([], null));
    }
    sealed class AlteredEvents(IDomainEventReader inner, Func<DomainEventRecord, DomainEventRecord> alter) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, fromOffset, ct))
                yield return alter(record);
        }
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
                yield return alter(record);
        }
    }
}
