using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Queries;

[Discriminator("bdgrz.commitment.version.get", 1)]
public sealed record GetCommitmentVersion(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Version)
    : IRequest<CommitmentVersionView>, IProgramScopedRequest, ICallable;
