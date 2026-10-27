using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;

/// <summary>Previews the exact selected source content in an unshared advisor handoff.</summary>
[Discriminator("bdgrz.advisor-collaboration.preview-advisor-handoff", 1)]
public sealed record PreviewAdvisorHandoff(Uuid TenantId, Uuid ProgramId, Uuid HandoffId)
    : IRequest<AdvisorHandoffPreviewView>, IProgramScopedRequest, ICallable;
