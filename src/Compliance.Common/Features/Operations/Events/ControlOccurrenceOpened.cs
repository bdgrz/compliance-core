using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     The one materialization of a control occurrence for its business window. <paramref name="CreationIntent" />
///     is the complete frozen creation intent; <paramref name="Intent" /> repeats the reference the opening
///     operation accepted. A direct opening carries the first response; a materialization receiver fact does not.
/// </summary>
[Discriminator("bdgrz.control.occurrence.opened", 2)]
public sealed record ControlOccurrenceOpened(Uuid TenantId, Uuid ProgramId, Uuid ControlId, Uuid OccurrenceId,
    long Revision, string Kind, Uuid ControlVersionId, Uuid PlanVersionId, DateOnly? PeriodStart, DateOnly? PeriodEnd,
    DateOnly? DueOn, string? Trigger, OperatingHolder Assignee, ActorReference OpenedBy, DateTimeOffset OpenedAt,
    OperationProvenance Operation, ControlBusinessWindow BusinessWindow, ImmutableContentRef CreationIntent,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
