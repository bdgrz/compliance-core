using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Opens one draft advisor handoff. <paramref name="HandoffId" /> is the logical operation identity and the
///     handoff identity, so an exact retry of the creation replays the original result and a different creation under
///     the same identity conflicts.
/// </summary>
[Discriminator("bdgrz.advisor-collaboration.create-advisor-handoff", 2)]
public sealed record CreateAdvisorHandoff(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    IReadOnlyList<AdvisorRecordVersion> RecordVersions, string Purpose, DateOnly ExpiresOn,
    Uuid? ComparedToHandoffId = null) : IRequest, IProgramScopedRequest,
    IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => HandoffId;
}
