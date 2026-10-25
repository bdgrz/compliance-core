using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FirmStaffNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateRawPrefixGivenCapturedNativeStaffCut(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, false, "Original intermediate status");
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, true, "Unchanged latest status");
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        var facts = await FactsAsync(host, owner);
        if (fault == "truncate")
            facts = facts[..^1];
        else
        {
            var original = (FirmStaffChangeRecorded)facts[^2];
            var changed = fault == "payload" ? original with { Reason = "A different intermediate status" } : original with { };
            changed.AttachMetadata(fault == "event_id" ? original.Metadata with { EventId = Uuid.CreateVersion4() } : original.Metadata);
            facts[^2] = changed;
        }

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await FirmStaffHistory.ReadAsync(new Records(owner.Stream, facts), owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(error);
        else
            Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3UL, owner.Revision);
        Assert.Equal(3UL, (await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId))).Revision);
    }

    [Fact]
    public async Task ShouldRejectUnselectedDuplicateLogicalStatusGivenInstalledDefaultReader()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, false, "First status");
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        var original = owner.Latest!;
        var duplicate = original with
        {
            Revision = owner.Revision + 1,
            Operation = original.Operation with { OriginalExpectedRevision = owner.Revision },
        };
        duplicate.AttachMetadata(original.Metadata with
        {
            EventId = Uuid.CreateVersion4(),
            AggregateVersion = owner.Version + 1,
        });
        await host.Scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(owner.Stream,
            owner.CommittedStreamPosition, [duplicate], CancellationToken.None);

        // Act
        var failure = await Record.ExceptionAsync(async () => _ = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId)));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Contains("repeated logical operation", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPreserveCapturedEarlierStaffCutGivenLaterValidStatusAppend()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        var captured = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        await FirmStaffFacts.SetStatusAsync(host.Provider, staff.StaffMemberId, false);

        // Act
        var history = await FirmStaffHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), captured);

        // Assert
        Assert.Single(history.Changes);
        Assert.True(history.Matches(captured.Latest!));
        Assert.Equal(1UL, captured.Revision);
        Assert.Equal(2UL, (await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId))).Revision);
    }

    [Fact]
    public async Task ShouldRejectForgedRegistrationCheckpointGivenActualCommittedNativeSource()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await host.RegisterStaffAsync();
        var owner = await host.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
        var original = owner.Accepted!;
        var forged = original with { Intent = original.Intent with { Sha256 = new string('f', 64) } };
        forged.AttachMetadata(original.Metadata);
        var services = host.Scope.ServiceProvider;

        // Act
        var failure = await Record.ExceptionAsync(() => FirmStaffSource.VerifyAsync(
            services.GetRequiredService<ISourceFactResolver>(), host.Content, owner.Stream, 0, forged, CancellationToken.None));

        // Assert
        Assert.IsType<SourceFactVerificationException>(failure);
        Assert.Equal(1UL, owner.Revision);
        Assert.Equal(owner.Id, original.Metadata.AggregateId);
        Assert.NotEqual(staff.StaffMemberId, owner.Id);
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, FirmStaffRecord owner)
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
