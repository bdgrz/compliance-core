using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Records the client's response on a feedback thread at the expected thread revision.</summary>
[Discriminator("bdgrz.advisor-collaboration.respond-to-advisor-feedback", 2)]
public sealed record RespondToAdvisorFeedback(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, string Response, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
