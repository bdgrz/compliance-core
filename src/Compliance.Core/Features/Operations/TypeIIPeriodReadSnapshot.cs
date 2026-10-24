using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Verified immutable read composition at the hydrated owner's exact native sequence.</summary>
public sealed class TypeIIPeriodReadSnapshot(TypeIIPeriod owner, TypeIIPeriodRules rules,
    IReadOnlyList<DomainEvent> facts)
{
    public TypeIIPeriod Owner { get; } = owner;
    public TypeIIPeriodRules Rules { get; } = rules;
    public Uuid Id => Owner.Id;
    public EventStreamAddress Stream => Owner.Stream;
    public long Revision => Owner.Revision;
    public DomainEvent? Pending => Owner.Pending;
    public Uuid? PendingOperationId => Owner.PendingOperationId;
    public TypeIIPeriodCalendarAuthorization? CalendarAuthorization => Owner.CalendarAuthorization;
    public TypeIIPeriodView? View => Rules.Current;
    public IReadOnlyList<DueControlOccurrencesGenerated> OccurrenceGenerations => Rules.OccurrenceGenerations;
    public DomainEvent? AcceptedFor(Uuid operationId) => facts.FirstOrDefault(value => value switch
    {
        TypeIIPeriodOpeningAccepted opening => opening.Operation.RootOperationId == operationId,
        TypeIIPeriodAmendmentAccepted amendment => amendment.Operation.RootOperationId == operationId,
        _ => false,
    });
    public DomainEvent? TerminalFor(Uuid operationId) => facts.FirstOrDefault(value => value is
        ObservationPeriodStarted or ObservationPeriodAmended or TypeIIPeriodOpeningRejected or TypeIIPeriodAmendmentCanceled &&
        value is Bdgrz.Compliance.Features.NativeOperations.INativeOperationFact fact && fact.Operation.RootOperationId == operationId);
    public DomainEvent? FactById(Uuid eventId) => facts.FirstOrDefault(value => value.Metadata?.EventId == eventId);
}
