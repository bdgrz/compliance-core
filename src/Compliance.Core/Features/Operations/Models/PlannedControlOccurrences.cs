using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>One planned control's current plan, display identifier, and occurrences through a horizon.</summary>
public sealed record PlannedControlOccurrences(Uuid ControlId, ControlOperatingPlanView Plan,
    string Identifier, IReadOnlyList<ControlOccurrenceView> Occurrences);
