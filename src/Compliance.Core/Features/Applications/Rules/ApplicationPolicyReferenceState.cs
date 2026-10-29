using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Models;
namespace Bdgrz.Compliance.Features.Applications.Rules;

sealed record ApplicationPolicyReferenceContent(string Title,
    IReadOnlyList<PolicyApplicabilityReference> Applicability);

sealed record ApplicationPolicyReferenceState(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, long Revision, ApplicationPolicyReferenceContent? DraftContent,
    string? DraftContentSha256, long? DraftPredecessorVersion,
    ApplicationPolicyReferenceContent? CurrentContent, long? CurrentVersion,
    DateOnly? CurrentEffectiveFrom, DateOnly? PendingRetirementEffectiveUntil, bool Retired,
    int HistoricalUnresolvedReferenceCount, int UnresolvedReferenceCount);

sealed record ApplicationPolicyReferenceCoverage(Uuid TenantId, long UnresolvedReferenceCount);
