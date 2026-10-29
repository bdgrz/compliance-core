using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>An exact finding revision read from its authoritative owner before linking.</summary>
public sealed record AuditObservationFindingReference(Uuid TenantId, Uuid ProgramId,
    Uuid FindingId, long Revision, FindingSource Source);
