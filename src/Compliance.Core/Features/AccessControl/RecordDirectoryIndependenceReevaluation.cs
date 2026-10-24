using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Internal source-verifying effect; neither a public request nor acceptance authority. It targets exactly one
///     engagement acceptance pair: the directory fan-out and discovery belong to the reconciliation reactors, never to
///     a tenant-wide mutation. <paramref name="CommandId" /> derives from the directory decision, the accepted fact and
///     the staff identity, so an exact redelivery is the same operation.
/// </summary>
[Discriminator("bdgrz.independence.record-directory-reevaluation", 1)]
sealed record RecordDirectoryIndependenceReevaluation(Uuid CommandId, Uuid TenantId,
    ulong DirectoryResourceOffset, Uuid DirectoryEventId, string DirectoryPayloadSha256,
    ulong AcceptanceResourceOffset, Uuid AcceptanceEventId, Uuid AcceptanceRequestId,
    string AcceptancePayloadSha256, Uuid StaffMemberId, Uuid UserId, Uuid EngagementId, long AcceptedServiceEpoch)
    : IRequest, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;

    /// <summary>The deterministic command id of the reevaluation of one accepted fact by one directory decision.</summary>
    public static Uuid CommandIdOf(Uuid directoryEventId, Uuid acceptanceEventId, Uuid staffMemberId, Uuid userId) =>
        Bdgrz.Compliance.Features.Kernel.EffectIdentity.CommandId(directoryEventId,
            "RecordDirectoryIndependenceReevaluation", $"{acceptanceEventId}:{staffMemberId}:{userId}", null);
}
