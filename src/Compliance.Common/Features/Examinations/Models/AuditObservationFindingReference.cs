using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>An exact finding revision read from its authoritative owner before linking.</summary>
public sealed record AuditObservationFindingReference(Uuid TenantId, Uuid ProgramId,
    Uuid FindingId, long Revision, FindingSource Source);
