using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>The verification and closure review decision for a finding.</summary>
public sealed record FindingClosureView(Uuid DecisionId, string VerificationRationale,
    IReadOnlyList<EvidenceReference> ResolutionEvidence, string Rationale, Uuid CloserMemberId,
    ActorReference ClosedBy, DateTimeOffset ClosedAt, Uuid? SeparationOfDutiesWaiverId = null);
