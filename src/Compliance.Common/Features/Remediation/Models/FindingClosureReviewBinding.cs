using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>The exact proposal revision, reviewer and optional waiver accepted for final closure.</summary>
public sealed record FindingClosureReviewBinding(Uuid ClosureId, long ProposalRevision, Uuid ReviewerMemberId,
    ActorReference ReviewedBy, DateTimeOffset ReviewedAt, SeparationOfDutiesWaiverProof? Waiver);
