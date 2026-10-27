using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>Revises a draft or rejected response at the revision of that response owner.</summary>
[Discriminator("bdgrz.auditor-requests.revise-audit-response-draft", 2)]
public sealed record ReviseAuditResponseDraft(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    Uuid ResponseId, long ExpectedRevision, string Summary, IReadOnlyList<Uuid> EvidenceArtifactIds)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
