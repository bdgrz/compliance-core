using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Replaces the exact selected versions of an open handoff at the expected handoff revision.</summary>
[Discriminator("bdgrz.advisor-collaboration.revise-advisor-handoff-selection", 2)]
public sealed record ReviseAdvisorHandoffSelection(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    long ExpectedRevision, IReadOnlyList<AdvisorRecordVersion> RecordVersions, string ChangeReason)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
