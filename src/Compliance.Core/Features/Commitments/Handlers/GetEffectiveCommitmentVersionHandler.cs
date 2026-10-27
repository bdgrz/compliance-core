using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Queries;
namespace Bdgrz.Compliance.Features.Commitments.Handlers;

public sealed class GetEffectiveCommitmentVersionHandler(
    CommitmentVersionReadConsistency consistency)
    : IRequestHandler<GetEffectiveCommitmentVersion, CommitmentVersionView>
{
    public async ValueTask<Result<CommitmentVersionView>> HandleAsync(
        IRequestContext<GetEffectiveCommitmentVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await consistency.SourceAsync(request.TenantId, request.ProgramId,
            request.DraftId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<CommitmentVersionView>.Failure(source.Error);
        return source.Value.EffectiveVersionOn(request.EffectiveOn) is { } version
            ? await consistency.GetAsync(request.TenantId, request.ProgramId, request.DraftId,
                version, ct).ConfigureAwait(false)
            : Result<CommitmentVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "No commitment version was effective on that date."));
    }
}
