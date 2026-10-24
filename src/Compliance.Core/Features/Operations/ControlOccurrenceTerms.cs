using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>The business terms of a materialization, copied from its verified creation intent and plan.</summary>
public sealed record ControlOccurrenceTerms(string Kind, Uuid ControlVersionId, Uuid PlanVersionId,
    DateOnly? PeriodStart, DateOnly? PeriodEnd, DateOnly? DueOn, string? Trigger, OperatingHolder Assignee,
    ActorReference OpenedBy, DateTimeOffset OpenedAt, ControlBusinessWindow Window);
