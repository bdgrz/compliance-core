using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Records the client's response on a feedback thread at the expected thread revision.</summary>
[Discriminator("bdgrz.advisor-collaboration.respond-to-advisor-feedback", 2)]
public sealed record RespondToAdvisorFeedback(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, string Response, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
