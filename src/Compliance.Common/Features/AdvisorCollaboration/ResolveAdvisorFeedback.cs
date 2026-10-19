using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Closes the client-owned work raised from advisor feedback.</summary>
[Discriminator("bdgrz.advisor-collaboration.resolve-advisor-feedback", 1)]
public sealed record ResolveAdvisorFeedback(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest,
    ICallable;
