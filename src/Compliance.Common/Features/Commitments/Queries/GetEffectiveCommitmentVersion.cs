using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Commitments.Queries;

[Discriminator("bdgrz.commitment.version.effective.get", 1)]
public sealed record GetEffectiveCommitmentVersion(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, DateOnly EffectiveOn)
    : IRequest<CommitmentVersionView>, IProgramScopedRequest, ICallable;
