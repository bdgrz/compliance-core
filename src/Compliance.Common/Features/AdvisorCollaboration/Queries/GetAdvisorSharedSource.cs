using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;

/// <summary>Reads one source only when it is present in the requested open advisor handoff.</summary>
[Discriminator("bdgrz.advisor-collaboration.get-shared-source", 1)]
public sealed record GetAdvisorSharedSource(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    string SourceFamily, Uuid RecordId, Uuid? EntryId = null)
    : IRequest<AdvisorSharedSourceView>, IProgramReadRequest, ICallable;
