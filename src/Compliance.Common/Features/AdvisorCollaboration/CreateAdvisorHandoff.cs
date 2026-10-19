using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the create advisor handoff intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.create-advisor-handoff", 1)]
public sealed record CreateAdvisorHandoff(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    IReadOnlyList<AdvisorRecordVersion> RecordVersions, string Purpose, DateOnly ExpiresOn,
    Uuid? ComparedToHandoffId = null) : IRequest, IProgramScopedRequest,
    IClientManagementMutationRequest, ICallable;
