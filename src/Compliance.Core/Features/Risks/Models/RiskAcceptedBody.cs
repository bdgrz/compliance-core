using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record RiskAcceptedBody(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision, RiskAcceptanceView Acceptance)
{
    public long? OriginalExpectedRevision { get; init; }
    public string? AuthoredIntentSha256 { get; init; }
    public RiskMethodVersionView? Method { get; init; }
    public bool ApproverHoldsAuthority { get; init; }
    public Uuid? OwnerMemberId { get; init; }
    public SeparationOfDutiesWaiverProof? WaiverProof { get; init; }
}
