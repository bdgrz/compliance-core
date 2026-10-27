using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
namespace Bdgrz.Compliance.Features.Operations.Projections;

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
