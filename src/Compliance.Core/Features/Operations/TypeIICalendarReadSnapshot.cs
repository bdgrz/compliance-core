using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Current live ranges and only the terminal operations explicitly selected for this read.</summary>
public sealed class TypeIICalendarReadSnapshot(TypeIIObservationCalendar owner, TypeIICalendarAuthority authority,
    TypeIICalendarRules rules, IReadOnlyList<TypeIICalendarHistoryRead> history, IReadOnlyDictionary<long, DomainEvent> facts)
{
    public TypeIIObservationCalendar Owner { get; } = owner;
    public TypeIICalendarAuthority Authority { get; } = authority;
    public TypeIICalendarRules Rules { get; } = rules;
    public IReadOnlyList<TypeIICalendarHistoryRead> History { get; } = history;
    public long Revision => Owner.Revision;
    public long ReservationGeneration => Owner.ReservationGeneration;
    public EventStreamAddress Stream => Owner.Stream;
    public IReadOnlyCollection<TypeIIRangeReservation> Reservations => Rules.Reservations.Select(Compose).ToArray();
    public TypeIIRangeReservation? Find(Uuid root) => Rules.Find(root) is { } value ? Compose(value) : null;
    public TypeIIRangeReservation? FindByAuthorization(Uuid eventId) => Reservations.FirstOrDefault(value => value.AuthorizedFact?.Metadata.EventId == eventId);
    public TypeIIRangeReservation? FindOverlap(Uuid period, ObservationRange range) => Rules.FindOverlap(period, range) is { } value ? Compose(value) : null;
    TypeIIRangeReservation Compose(TypeIICalendarReservationContent value) => new(value.OperationId, value.PeriodId, value.State,
        value.Kind, value.Range, value.Generation, value.PriorOperationId,
        value.AuthorizationRevision is { } authorized ? (TypeIIPeriodRangeAuthorized)facts[authorized] : null,
        value.SettlementRevision is { } settled ? (TypeIIPeriodRangeSettled)facts[settled] : null);
}
