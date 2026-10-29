using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Commitments.Queries;

[Discriminator("bdgrz.commitment.decision.list", 1)]
public sealed record ListCommitmentDecisions(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<CommitmentDecisionView>>, IProgramScopedRequest, ICallable;
