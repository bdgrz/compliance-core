using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>Verified current handling and assessment identities without live narrative bodies.</summary>
public sealed record ComplianceIncidentReadAuthority(ComplianceIncident Owner, ComplianceIncidentCurrentAuthority Authority);
