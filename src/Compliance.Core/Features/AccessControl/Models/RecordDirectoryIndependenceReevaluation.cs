using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

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
        Bdgrz.Compliance.Features.Kernel.Services.EffectIdentity.CommandId(directoryEventId,
            "RecordDirectoryIndependenceReevaluation", $"{acceptanceEventId}:{staffMemberId}:{userId}", null);
}
