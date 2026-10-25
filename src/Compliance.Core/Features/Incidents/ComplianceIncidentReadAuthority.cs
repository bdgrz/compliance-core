namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Verified current handling and assessment identities without live narrative bodies.</summary>
public sealed record ComplianceIncidentReadAuthority(ComplianceIncident Owner, ComplianceIncidentCurrentAuthority Authority);
