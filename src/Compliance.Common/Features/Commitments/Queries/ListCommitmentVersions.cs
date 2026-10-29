using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Commitments.Queries;

[Discriminator("bdgrz.commitment.version.list", 1)]
public sealed record ListCommitmentVersions(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<CommitmentVersionView>>, IProgramScopedRequest, ICallable;
