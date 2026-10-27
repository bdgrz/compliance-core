using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Closes the client-owned work raised from advisor feedback at the expected thread revision.</summary>
[Discriminator("bdgrz.advisor-collaboration.resolve-advisor-feedback", 2)]
public sealed record ResolveAdvisorFeedback(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
