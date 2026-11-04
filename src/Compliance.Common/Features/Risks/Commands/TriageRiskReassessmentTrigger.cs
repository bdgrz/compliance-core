using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Risks.Commands;

[Discriminator("bdgrz.risk.program_trigger.triage", 1)]
public sealed record TriageRiskReassessmentTrigger(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid TriggerId, long ExpectedRevision, IReadOnlyList<Uuid> AffectedRiskIds, string? ReasonWhenNone)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
