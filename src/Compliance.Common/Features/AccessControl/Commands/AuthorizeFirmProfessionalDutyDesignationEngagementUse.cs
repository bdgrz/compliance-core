using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Acquires the exact active engagement-partner designation the partner evaluation relies on for one personal engagement decision. <see cref="Source" /> is the accepted admission fact;
///     the receiver verifies it by hydrating the engagement owner and never trusts a caller-supplied policy value.
/// </summary>
[Discriminator("bdgrz.firm-professional-duty-designation.authorize-firm-professional-duty-designation-engagement-use", 1)]
public sealed record AuthorizeFirmProfessionalDutyDesignationEngagementUse(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid SourceOwnerId,
    Uuid SourceContentEventId, ulong ExpectedContentRevision, Uuid EngagementId)
    : ISourceUseAuthorization, IEngagementPersonalDecisionUse, IEngagementPersonalDecisionReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
