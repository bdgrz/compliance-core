using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

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
