using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Queries;
[Discriminator("bdgrz.commitment.governance.getcommitmentcontrollink", 1)]
public sealed record GetCommitmentControlLink(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, Uuid ControlId) : IRequest<CommitmentLinkStatus>, IProgramScopedRequest, ICallable;
