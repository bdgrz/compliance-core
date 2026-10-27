using Cntryl.Portia;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentAuthorityHeader(Uuid TenantId, Uuid ProgramId, Uuid DraftId, Uuid ServiceId,
    Uuid? ProviderId, string Kind, string Identifier, long Revision, bool IsCreated, bool IsRetired,
    string SourceReference, long EffectiveVersionCount, long? LatestEffectiveRevision, DateOnly? LatestEffectiveFrom,
    Uuid? AcceptedReviewDecisionId, Uuid? AcceptedReviewerMemberId, DateOnly? RetiredEffectiveOn, ImmutableContentRef Content);
