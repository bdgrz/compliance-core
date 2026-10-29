using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;

/// <summary>Closes the client-owned work raised from advisor feedback at the expected thread revision.</summary>
[Discriminator("bdgrz.advisor-collaboration.resolve-advisor-feedback", 2)]
public sealed record ResolveAdvisorFeedback(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, long ExpectedRevision)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
