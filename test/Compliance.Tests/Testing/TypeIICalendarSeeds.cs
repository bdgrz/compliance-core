using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Publishes each pure range decision through its actual immutable native owner cut.</summary>
static class TypeIICalendarSeeds
{
    public static Task<TypeIIObservationCalendar> WriteAsync(IAggregateReader reader, IAggregateWriter writer,
        IDomainEventReader events, IImmutableContentStore content, TypeIICalendarRules source, CancellationToken ct = default) =>
        SaveAsync(reader, writer, events, content, source, source.AppliedChanges.ToArray(), ct);

    public static Task<TypeIIObservationCalendar> AppendAsync(IAggregateReader reader, IAggregateWriter writer,
        IDomainEventReader events, IImmutableContentStore content, TypeIICalendarRules source, CancellationToken ct = default) =>
        SaveAsync(reader, writer, events, content, source, source.PendingChanges.ToArray(), ct);

    static async Task<TypeIIObservationCalendar> SaveAsync(IAggregateReader reader, IAggregateWriter writer,
        IDomainEventReader events, IImmutableContentStore content, TypeIICalendarRules source, IReadOnlyList<object> changes, CancellationToken ct)
    {
        var calendars = new TypeIICalendarReader(reader, events, content);
        TypeIIObservationCalendar? owner = null;
        foreach (var change in changes)
        {
            var snapshot = await calendars.ReadAsync(source.TenantId, source.Id, TypeIICalendarContent.RequiredRoots(change), ct: ct);
            var rules = snapshot.Rules;
            var decision = change switch
            {
                TypeIICalendarAuthorizationChange value => rules.Authorize(value.Operation, value.Intent, value.PeriodId,
                    value.Kind, value.NewRange, value.PriorOperationId),
                TypeIICalendarSettlementChange value => rules.Settle(value.Operation, value.Intent, value.PeriodId,
                    value.Settlement, value.TargetOperationId, value.ReleasedOperationId),
                _ => throw new InvalidOperationException("Another calendar fixture mutation kind."),
            };
            Assert.True(decision.IsSuccess, decision.Error?.Message);
            var prepared = await TypeIICalendarContent.FreezeAsync(content, snapshot, rules, ct);
            owner = snapshot.Owner;
            var appended = owner.Append(TypeIICalendarContent.Fact(prepared), prepared, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(appended.IsSuccess, appended.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
        source.ClearPendingChanges();
        return owner ?? await reader.HydrateAsync(new TypeIIObservationCalendar(source.TenantId, source.Id), ct);
    }
}
