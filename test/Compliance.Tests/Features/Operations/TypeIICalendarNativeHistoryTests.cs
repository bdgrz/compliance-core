using System.Text;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class TypeIICalendarNativeHistoryTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ShouldRejectUnprovenTerminalMembershipOrAbsenceGivenDamagedCalendarHistoryHead(bool unavailable, bool absent)
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var closed = await fixture.StartPeriodAsync();
        await fixture.FreezeCloseAsync(closed);
        await fixture.Host.ReleaseAsync<ObservationPeriodCloseFrozen>(fixture.StreamOf(closed));
        var current = await fixture.CalendarAsync();
        var head = current.Authority.ReleasedHistory!;
        var pending = await fixture.AcceptOpeningAsync(Uuid.CreateVersion4());
        var period = await fixture.PeriodAsync(pending);
        var accepted = period.AcceptedFor(pending)!;
        var intentReference = TypeIIPeriod.IntentOf(accepted)!;
        var intent = await fixture.Host.Content.ReadJsonAsync<TypeIIRangeIntent>(fixture.TenantId.ToString(), intentReference);
        var commandId = TypeIIRangeEffects.ReceiverCommandId(accepted.Metadata.EventId, nameof(AcquireTypeIIPeriodRange), intent.Header.Episode);
        var command = new AcquireTypeIIPeriodRange(commandId, NativeFactReference.Of(period.Owner.Stream, accepted),
            fixture.CalendarStream, pending, TypeIIRangeEffects.Generation, null, intentReference, pending, intent.OldRange, intent.NewRange);
        await DamageAsync(fixture.Host.Services.GetRequiredService<IKvClient>(), fixture.TenantId.ToString(), head, unavailable);

        // Act
        Result<ReceiverOutcome>? acquisition = null;
        Exception? failure = null;
        if (absent)
            acquisition = await fixture.Host.Services.GetRequiredService<TypeIIRangeReceivers>().AcquireAsync(
                new RequestDispatchContext(RequestActor.System), command, CancellationToken.None);
        else
            failure = await Record.ExceptionAsync(async () =>
                await fixture.Host.Calendars.ReadAsync(fixture.TenantId, fixture.ProgramId, [closed]));
        var after = await fixture.CalendarAsync();
        var facts = await fixture.Host.EventsAsync<DomainEvent>(fixture.CalendarStream);

        // Assert
        if (absent)
        {
            var outcome = Assert.IsType<Result<ReceiverOutcome>>(acquisition);
            Assert.False(outcome.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, outcome.Error?.Kind);
            Assert.Equal(unavailable, outcome.Error!.IsTransient);
        }
        else if (unavailable)
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(current.Revision, after.Revision);
        Assert.Equal(current.Owner.NativeSequence, after.Owner.NativeSequence);
        Assert.Equal(current.Owner.CurrentAuthority, after.Owner.CurrentAuthority);
        Assert.Empty(after.History);
        Assert.Empty(after.Reservations);
        Assert.Equal(3, facts.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReplayActualAcquisitionBeforeImmutableCurrentReadGivenDamagedLaterCalendarAuthority(bool unavailable)
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var root = await fixture.StartPeriodAsync();
        var period = await fixture.PeriodAsync(root);
        var accepted = period.AcceptedFor(root)!;
        var intentReference = TypeIIPeriod.IntentOf(accepted)!;
        var intent = await fixture.Host.Content.ReadJsonAsync<TypeIIRangeIntent>(fixture.TenantId.ToString(), intentReference);
        var commandId = TypeIIRangeEffects.ReceiverCommandId(accepted.Metadata.EventId, nameof(AcquireTypeIIPeriodRange), intent.Header.Episode);
        var command = new AcquireTypeIIPeriodRange(commandId, NativeFactReference.Of(period.Stream, accepted), fixture.CalendarStream,
            root, TypeIIRangeEffects.Generation, null, intentReference, root, intent.OldRange, intent.NewRange);
        var calendar = await fixture.CalendarAsync(root);
        var source = calendar.Find(root)!.AuthorizedFact!;
        Assert.Equal(commandId, source.Operation.LogicalId);
        await DamageAsync(fixture.Host.Services.GetRequiredService<IKvClient>(), fixture.TenantId.ToString(), calendar.Owner.CurrentAuthority!, unavailable);

        // Act
        var result = await fixture.Host.Services.GetRequiredService<TypeIIRangeReceivers>().AcquireAsync(
            new RequestDispatchContext(RequestActor.System), command, CancellationToken.None);
        var owner = await fixture.Host.Reader.HydrateAsync(new TypeIIObservationCalendar(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var replay = Assert.IsType<ReceiverOutcome.Replayed>(result.Value);
        Assert.Equal(NativeFactReference.Of(fixture.CalendarStream, source), replay.Fact);
        Assert.Equal(calendar.Owner.NativeSequence, owner.NativeSequence);
        Assert.Equal(calendar.Owner.CurrentAuthority, owner.CurrentAuthority);
        Assert.Equal(2, (await fixture.Host.EventsAsync<DomainEvent>(fixture.CalendarStream)).Count);
    }

    [Fact]
    public async Task ShouldRefuseUnprovenAbsenceAtNativeAppendGivenTruncatedTerminalEvidence()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var closed = await fixture.StartPeriodAsync();
        await fixture.FreezeCloseAsync(closed);
        await fixture.Host.ReleaseAsync<ObservationPeriodCloseFrozen>(fixture.StreamOf(closed));
        var pending = await fixture.AcceptOpeningAsync(Uuid.CreateVersion4());
        var period = await fixture.PeriodAsync(pending);
        var accepted = period.AcceptedFor(pending)!;
        var intentReference = TypeIIPeriod.IntentOf(accepted)!;
        var intent = await fixture.Host.Content.ReadJsonAsync<TypeIIRangeIntent>(fixture.TenantId.ToString(), intentReference);
        var commandId = TypeIIRangeEffects.ReceiverCommandId(accepted.Metadata.EventId, nameof(AcquireTypeIIPeriodRange), intent.Header.Episode);
        var operation = TypeIIRangeEffects.Child(commandId, nameof(AcquireTypeIIPeriodRange), accepted.Metadata.EventId,
            intent, fixture.CalendarStream, TypeIIRangeFixture.StartedAt);
        var selected = await fixture.Host.Calendars.ReadAsync(fixture.TenantId, fixture.ProgramId, [pending]);
        Assert.True(selected.Rules.Authorize(operation, intentReference, pending, intent.OperationKind, intent.NewRange, null).IsSuccess);
        var prepared = await TypeIICalendarContent.FreezeAsync(fixture.Host.Content, selected, selected.Rules);
        Assert.NotEmpty(prepared.History);
        var owner = selected.Owner;
        var revision = owner.Revision;

        // Act
        var outcome = owner.Append(TypeIICalendarContent.Fact(prepared), prepared with { History = [] }, owner.NativeSequence, owner.CommittedStreamPosition);
        var persisted = await fixture.CalendarAsync(pending);

        // Assert
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, outcome.Error?.Kind);
        Assert.False(outcome.Error!.IsTransient);
        Assert.Equal(revision, owner.Revision);
        Assert.Empty(new AggregateScenario<TypeIIObservationCalendar>(owner).PendingEvents);
        Assert.Null(persisted.Find(pending));
        Assert.Equal(revision, persisted.Revision);
        Assert.Equal(3, (await fixture.Host.EventsAsync<DomainEvent>(fixture.CalendarStream)).Count);
    }

    static async Task DamageAsync(IKvClient client, string realm, ImmutableContentRef reference,
        bool unavailable)
    {
        var hex = reference.ContentId.ToString().Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        await using var transaction = await client.BeginAsync($"kv://bdgrz/immutable-content/{realm}-{hex[..2]}", KvMode.ReadWrite);
        if (unavailable)
            await transaction.DeleteAsync(Encoding.UTF8.GetBytes($"m/{hex}"));
        else
        {
            var key = Encoding.UTF8.GetBytes($"c/{hex}/000000");
            var stored = await transaction.GetAsync(key);
            Assert.True(stored.Found);
            var bytes = stored.Value!.Value.ToArray();
            bytes[0] ^= 1;
            await transaction.PutAsync(key, bytes);
        }
        await transaction.CommitAsync(KvDurability.Sync);
    }
}
