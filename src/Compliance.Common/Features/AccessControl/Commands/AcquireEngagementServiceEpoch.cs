using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.AccessControl.Commands;

/// <summary>
///     Acquires the exact complete service history and epoch an acceptance or staff assignment relies on.
///     <see cref="Source" /> is the accepted root admission; the receiver verifies it by hydrating the engagement
///     owner and never trusts a caller-supplied policy value. A changed epoch rejects the whole operation.
/// </summary>
[Discriminator("bdgrz.client-service-independence.acquire-engagement-service-epoch", 1)]
public sealed record AcquireEngagementServiceEpoch(Uuid CommandId, NativeFactReference Source, EventStreamAddress Target,
    Uuid OperationId, long Generation, long? ExpectedRevision, ImmutableContentRef Intent, Uuid EngagementId,
    long ServiceEpoch)
    : ISourceUseAuthorization, IEngagementPersonalDecisionUse, IEngagementAcceptanceReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    Uuid ISourceUseAuthorization.SourceOwnerId => Uuid.Parse(Target.Resource, System.Globalization.CultureInfo.InvariantCulture);

    Uuid ISourceUseAuthorization.SourceContentEventId => Uuid.Empty;

    ulong ISourceUseAuthorization.ExpectedContentRevision => (ulong)ServiceEpoch;
}
