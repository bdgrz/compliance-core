using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed record ApplicationPolicyReferenceContent(string Title,
    IReadOnlyList<PolicyApplicabilityReference> Applicability);

sealed record ApplicationPolicyReferenceState(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, long Revision, ApplicationPolicyReferenceContent? DraftContent,
    string? DraftContentSha256, long? DraftPredecessorVersion,
    ApplicationPolicyReferenceContent? CurrentContent, long? CurrentVersion,
    DateOnly? CurrentEffectiveFrom, DateOnly? PendingRetirementEffectiveUntil, bool Retired,
    int HistoricalUnresolvedReferenceCount, int UnresolvedReferenceCount);

sealed record ApplicationPolicyReferenceCoverage(Uuid TenantId, long UnresolvedReferenceCount);
