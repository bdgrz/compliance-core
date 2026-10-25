using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ProfessionalDutyNativeHistoryTests
{
    [Theory]
    [InlineData("payload")]
    [InlineData("event_id")]
    [InlineData("truncate")]
    public async Task ShouldRejectAlternateMiddlePrefixGivenActualHydratedDesignationCut(string fault)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var state = await SeedAsync(host);
        var facts = await FactsAsync(host, state.Owner);
        if (fault == "truncate")
            facts = facts[..^1];
        else if (fault == "event_id")
        {
            var original = (FirmProfessionalDutyDesignationRecorded)facts[3];
            var changed = original with { };
            changed.AttachMetadata(original.Metadata with { EventId = Uuid.CreateVersion4() });
            facts[3] = changed;
        }
        else
        {
            var admission = (ProfessionalDutyChangeAccepted)facts[2];
            var terminal = (FirmProfessionalDutyDesignationRecorded)facts[3];
            var different = admission.Intent with { ContentId = Uuid.CreateVersion4() };
            var admitted = admission with { Intent = different };
            admitted.AttachMetadata(admission.Metadata);
            var completed = terminal with { Intent = different };
            completed.AttachMetadata(terminal.Metadata);
            facts[2] = admitted;
            facts[3] = completed;
        }

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await ProfessionalDutyHistory.ReadAsync(new Records(state.Owner.Stream, facts), state.Owner));

        // Assert
        if (fault == "truncate")
            Assert.IsType<ImmutableContentUnavailableException>(error);
        else
            Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.Equal(3UL, state.Owner.Revision);
        Assert.Equal(3UL, (await host.HydrateAsync(new FirmProfessionalDutyDesignation(state.User, FirmProfessionalDuty.RuleRatifier, null))).Revision);
    }

    [Fact]
    public async Task ShouldSelectEarlierOriginalReceiptGivenLaterSignedDesignationRefreshes()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var state = await SeedAsync(host);

        // Act
        var selected = await host.HydrateAsync(new FirmProfessionalDutyDesignation(state.User, FirmProfessionalDuty.RuleRatifier,
            null, state.First, state.First));
        var history = await ProfessionalDutyHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), selected);

        // Assert
        Assert.Equal(3UL, selected.Revision);
        Assert.Equal(history.AcceptedFor(state.First), selected.AcceptedFor(state.First));
        Assert.Equal(history.TerminalFor(state.First), selected.TerminalFor(state.First));
        Assert.NotNull(selected.Acceptance.SelectedFact);
        Assert.Equal(6, history.Facts.Count);
        Assert.NotEqual(Assert.IsAssignableFrom<DomainEvent>(selected.Latest).Metadata.EventId,
            selected.TerminalFor(state.First)!.Metadata.EventId);
    }

    static async Task<Seed> SeedAsync(IndependenceHost host)
    {
        var staff = await host.RegisterStaffAsync();
        var designation = Uuid.CreateVersion4();
        Uuid? previous = null;
        Uuid first = default;
        for (var index = 1; index <= 3; index++)
        {
            var signed = ProfessionalDutyAssertionTestIssuer.Issue(designation, staff.UserId, FirmProfessionalDuty.RuleRatifier,
                null, index, "active", previous, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30));
            Assert.True((await ProfessionalDutyFacts.RecordAsync(host.Provider, signed, staff, staff.Actor, DateTimeOffset.UtcNow)).IsSuccess);
            first = index == 1 ? signed.Evidence.AssertionId : first;
            previous = signed.Evidence.AssertionId;
        }
        return new Seed(staff.UserId, first,
            await host.HydrateAsync(new FirmProfessionalDutyDesignation(staff.UserId, FirmProfessionalDuty.RuleRatifier, null)));
    }

    static async Task<DomainEvent[]> FactsAsync(IndependenceHost host, FirmProfessionalDutyDesignation owner)
    {
        var found = new List<DomainEvent>();
        await foreach (var record in host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>().ReadAsync(owner.Stream, 0))
            found.Add(record.Event);
        return found.ToArray();
    }

    sealed record Seed(Uuid User, Uuid First, FirmProfessionalDutyDesignation Owner);

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
