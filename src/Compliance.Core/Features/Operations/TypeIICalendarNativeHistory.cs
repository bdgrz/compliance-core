using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
/// Full transient validation of the actual raw native transitions. No historical immutable authority body is needed,
/// including when an accepted receiver retries. Only a complete exact physical and metadata cut proves its result.
/// </summary>
static class TypeIICalendarNativeHistory
{
    public static async ValueTask<TypeIICalendarRules> ReadAsync(TypeIIObservationCalendar owner,
        IDomainEventReader events, CancellationToken ct)
    {
        var rules = new TypeIICalendarRules(owner.TenantId, owner.Id);
        if (owner.Header is null && owner.Version == 0 && owner.CommittedStreamPosition == 0)
            return rules;
        var physical = 0UL;
        var metadataVersion = 0UL;
        var nativeSequence = 0L;
        var eventIds = new HashSet<Uuid>();
        DomainEvent? last = null;
        await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (record.ResourceOffset >= owner.CommittedStreamPosition)
                break;
            var value = record.Event;
            var metadata = TypeIICalendarContent.Metadata(value);
            var header = TypeIICalendarContent.FactHeader(value);
            if (record.Stream != owner.Stream || record.ResourceOffset != physical || metadata.IsAudit ||
                metadata.EventId == Uuid.Empty || !eventIds.Add(metadata.EventId) || metadata.AggregateId != owner.Id ||
                metadata.AggregateVersion != checked(metadataVersion + 1) || header is null ||
                header.TenantId != owner.TenantId || header.ProgramId != owner.Id ||
                value is not INativeOperationFact { Operation: { } operation } || operation.Owner != owner.Stream)
                throw Invalid("The calendar native transition has another exact owner, physical offset or metadata identity.");
            var result = value switch
            {
                TypeIIPeriodRangeAuthorized fact when fact.Owner == owner.Stream && fact.Intent is not null &&
                    fact.Generation == TypeIIRangeEffects.Generation && fact.Revision == header.Revision =>
                    rules.Authorize(fact.Operation, fact.Intent, fact.PeriodId, fact.Kind, fact.NewRange, fact.PriorOperationId),
                TypeIIPeriodRangeSettled fact when fact.Owner == owner.Stream && fact.Intent is not null &&
                    fact.Generation == TypeIIRangeEffects.Generation && fact.Revision == header.Revision =>
                    rules.Settle(fact.Operation, fact.Intent, fact.PeriodId, fact.Settlement, fact.TargetOperationId, fact.ReleasedOperationId),
                _ => Result.Failure(new RequestError(RequestErrorKind.Conflict, "Another calendar native transition kind.")),
            };
            if (!result.IsSuccess)
                throw Invalid("The calendar native transition violates its actual reservation history.");
            var expected = new TypeIIObservationCalendarHeader(owner.TenantId, owner.Id, rules.Revision,
                rules.ReservationGeneration, rules.Reservations.Count(reservation => reservation.IsLive),
                rules.Reservations.Count(reservation => !reservation.IsLive));
            if (header != expected || !TypeIICalendarContent.Reference(TypeIICalendarContent.FactAuthority(value),
                    TypeIICalendarContent.AuthoritySchema))
                throw Invalid("The calendar native transition header differs from its exact replayed range authority.");
            if (operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != value.GetType() ||
                    !selected.Equals(value) || selected.Metadata != metadata))
                throw Invalid("The calendar selected native transition differs from its actual acceptance and metadata.");
            rules.ClearPendingChanges();
            physical = checked(record.ResourceOffset + 1);
            metadataVersion = metadata.AggregateVersion;
            nativeSequence = checked(nativeSequence + 1);
            last = value;
        }
        if (physical != owner.CommittedStreamPosition || metadataVersion != owner.Version ||
            nativeSequence != owner.NativeSequence || last is null)
            throw new ImmutableContentUnavailableException("The calendar native transition history has not reached its exact hydrated cut.");
        if (rules.Revision != owner.Revision || rules.ReservationGeneration != owner.ReservationGeneration ||
            TypeIICalendarContent.FactHeader(last) != owner.Header ||
            TypeIICalendarContent.FactAuthority(last) != owner.CurrentAuthority)
            throw Invalid("The calendar native transition history differs from its exact hydrated header and authority reference.");
        ct.ThrowIfCancellationRequested();
        return rules;
    }

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
