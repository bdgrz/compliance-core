using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Features.Incidents.Models;

/// <summary>Bounded discovery authority; current details are read from the exact native source.</summary>
public sealed record ComplianceIncidentDirectoryEntry(Uuid TenantId, Uuid ProgramId, Uuid ComplianceIncidentId,
    long NativeSequence, ComplianceIncidentAuthorityHeader Header);
