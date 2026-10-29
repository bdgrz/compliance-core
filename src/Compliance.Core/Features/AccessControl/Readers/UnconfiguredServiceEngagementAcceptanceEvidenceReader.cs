using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

sealed class UnconfiguredServiceEngagementAcceptanceEvidenceReader : IServiceEngagementAcceptanceEvidenceReader
{
    public ValueTask<Result<ServiceEngagementAcceptanceEvidence>> ReadCurrentAsync(Uuid tenantId,
        Uuid engagementId, Uuid actorUserId, CancellationToken ct) => ValueTask.FromResult(
        Result<ServiceEngagementAcceptanceEvidence>.Failure(new RequestError(RequestErrorKind.Conflict,
            "Current engagement authority and ratified rules are unavailable; acceptance was not recorded.", true)));
}
