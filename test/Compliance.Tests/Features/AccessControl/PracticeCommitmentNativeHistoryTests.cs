using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class PracticeCommitmentNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateHistoricalPrefixGivenCapturedPracticeAuthority(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        for (var index = 0; index < 3; index++)
            await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
                subject, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference, IndependenceHost.At);
        var owner = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff, subject));
        var facts = await FactsAsync(host, owner);
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = (PracticeIndependenceCommitted)facts[4];
            var changed = original with { };
            if (fault == "event_id")
                changed.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4() });
            else
            {
                var first = (PracticeIndependenceReserved)facts[3];
                var different = Uuid.CreateVersion4();
                var reserved = first with { EngagementId = different };
                reserved.AttachMetadata(first.Metadata);
                facts[3] = reserved;
                changed = original with { EngagementId = different };
                changed.AttachMetadata(original.Metadata);
            }
            facts[4] = changed;
        }

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await PracticeCommitmentHistory.ReadAsync(new Records(owner.Stream, facts), owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(3UL, owner.Revision);
        Assert.Equal("attest", owner.Practice);
    }

    [Fact]
    public async Task ShouldReadOriginalSelectedCommitmentGivenLaterSettledCommitments()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        var firstRoot = Uuid.CreateVersion4();
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
            subject, "attest", Uuid.CreateVersion4(), firstRoot, host.MemberReference, IndependenceHost.At);
        var stream = PracticeSubjects.Stream(host.Tenant, PracticeSubjectKind.Staff, subject);
        var key = new ReservationKey(firstRoot, stream, 1, stream, "actual_staff_assignment");
        var captured = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.Staff, subject, selectedReservation: key));
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
            subject, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference, IndependenceHost.At);

        // Act
        var original = await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), captured);
        var current = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.Staff, subject, selectedReservation: key));
        var history = await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), current);

        // Assert
        Assert.Single(original.Commitments);
        Assert.Equal(2, history.Commitments.Count);
        Assert.Equal(original.CommitmentOf(key), current.CommitmentOf(key));
        Assert.Equal(firstRoot, current.CommitmentOf(key)!.Operation.RootOperationId);
        Assert.Null(history.CommitmentOf(key with { FirstOwner = new ServiceEngagementIndependence(host.Tenant, Uuid.CreateVersion4()).Stream }));
        Assert.False(current.Uses.HasActiveUse);
        Assert.Equal(2, history.Assignments().Count);
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, PracticeIndependenceCommitment owner)
    {
        var found = new List<DomainEvent>();
        await foreach (var record in host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0))
            found.Add(record.Event);
        return found.ToArray();
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
