using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>Proposes a reviewed carry-forward plan from an exact completed Type II period.</summary>
[Discriminator("bdgrz.programs.propose-program-rollover", 2)]
public sealed record ProposeProgramRollover(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid RolloverId,
    ProgramRolloverContent Content)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
