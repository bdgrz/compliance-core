using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
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
