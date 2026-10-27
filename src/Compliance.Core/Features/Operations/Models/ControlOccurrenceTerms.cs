using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>The business terms of a materialization, copied from its verified creation intent and plan.</summary>
public sealed record ControlOccurrenceTerms(string Kind, Uuid ControlVersionId, Uuid PlanVersionId,
    DateOnly? PeriodStart, DateOnly? PeriodEnd, DateOnly? DueOn, string? Trigger, OperatingHolder Assignee,
    ActorReference OpenedBy, DateTimeOffset OpenedAt, ControlBusinessWindow Window);
