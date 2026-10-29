using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>An independent disposition of incident-related system-description review.</summary>
public sealed record ComplianceIncidentSystemDescriptionDecisionView(
    int ContentVersion,
    string Decision,
    Uuid DecisionId,
    string Rationale,
    Uuid DecidedByMemberId,
    string DecidedByDisplay,
    DateTimeOffset DecidedAt);
