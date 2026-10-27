using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
namespace Bdgrz.Compliance.Features.Operations.Projections;

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
        value is Bdgrz.Compliance.Features.NativeOperations.Contracts.INativeOperationFact fact && fact.Operation.RootOperationId == operationId);
    public DomainEvent? FactById(Uuid eventId) => facts.FirstOrDefault(value => value.Metadata?.EventId == eventId);
}
