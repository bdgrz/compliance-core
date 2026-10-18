using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed record ApplicationEvidenceSourceReferenceCoverage(Uuid TenantId, Uuid ProgramId,
    long UnresolvedReferenceCount);
