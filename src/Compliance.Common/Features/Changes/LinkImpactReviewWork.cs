using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Links the exact work identities selected by a current significant-change assessment.</summary>
[Discriminator("bdgrz.changes.link-impact-review-work", 2)]
public sealed record LinkImpactReviewWork(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ChangeId,
    int ContentVersion) : IRequest<UnitResult>, IIdentifiedRequest, IComplianceImpactReactionRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
