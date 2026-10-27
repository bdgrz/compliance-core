using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Commands;

/// <summary>Confirms the exact source-backed Type I baseline preview for an engagement.</summary>
[Discriminator("bdgrz.examinations.freeze-type-i-baseline", 1)]
public sealed record FreezeTypeIBaseline(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId,
    Uuid BaselineId, long ExpectedRevision, string ConfirmationRationale,
    string ExpectedPreviewSha256) : IRequest, IProgramScopedRequest,
    IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => BaselineId;
}
