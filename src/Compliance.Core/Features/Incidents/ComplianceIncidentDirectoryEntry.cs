using Bdgrz.Compliance.Features.Incidents.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Bounded discovery authority; current details are read from the exact native source.</summary>
public sealed record ComplianceIncidentDirectoryEntry(Uuid TenantId, Uuid ProgramId, Uuid ComplianceIncidentId,
    long NativeSequence, ComplianceIncidentAuthorityHeader Header);
