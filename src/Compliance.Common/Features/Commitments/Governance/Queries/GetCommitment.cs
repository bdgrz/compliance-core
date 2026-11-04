using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Queries;
[Discriminator("bdgrz.commitment.governance.getcommitment", 1)]
public sealed record GetCommitment(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId) : IRequest<CommitmentStatus>, IProgramScopedRequest, ICallable;
