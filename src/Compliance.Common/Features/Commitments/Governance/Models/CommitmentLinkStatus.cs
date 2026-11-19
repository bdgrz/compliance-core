using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Models;
public sealed record CommitmentLinkStatus(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, Uuid ControlId,
    long Revision, bool IsLinked, long CommitmentVersion, long ControlVersion, string? Rationale,
    bool Counts, bool NeedsConfirmation, bool InternallyPerformed);
