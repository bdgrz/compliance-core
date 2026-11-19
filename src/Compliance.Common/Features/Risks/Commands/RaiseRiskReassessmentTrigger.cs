using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.Risks.Commands;

[Discriminator("bdgrz.risk.program_trigger.raise", 1)]
public sealed record RaiseRiskReassessmentTrigger(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid TriggerId, string Kind, string Summary, SourceFactLocator? Source)
    : IRequest, ICallable;
