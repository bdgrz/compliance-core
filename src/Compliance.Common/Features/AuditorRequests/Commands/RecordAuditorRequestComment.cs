using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Commands;

/// <summary>
///     Admits one comment on the open request. <paramref name="CommentId" /> is the logical operation and the identity
///     of the comment's own thread; the complete body and compartment are frozen on the request and the thread is
///     materialized independently, so an exact retry never repeats the comment.
/// </summary>
[Discriminator("bdgrz.auditor-requests.record-auditor-request-comment", 2)]
public sealed record RecordAuditorRequestComment(Uuid CommentId, Uuid TenantId, Uuid ProgramId, Uuid RequestId,
    string Body, string Compartment, long ExpectedRequestRevision)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommentId;
}
