using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription.Handlers;

public sealed class GetSystemDescriptionHandler(SystemDescriptionReader descriptions, SystemDescriptionReadConsistency consistency)
    : IRequestHandler<GetSystemDescription, SystemDescriptionView>
{
    public async ValueTask<Result<SystemDescriptionView>> HandleAsync(IRequestContext<GetSystemDescription> context, CancellationToken ct)
    {
        var r = context.Request;
        try
        {
            var read = await descriptions.ReadAsync(r.TenantId, r.ProgramId, r.SystemDescriptionId, ct).ConfigureAwait(false);
            if (read.CurrentVersion is null)
                return Result<SystemDescriptionView>.Failure(new RequestError(RequestErrorKind.NotFound, "The system description was not found."));
            var confirmed = await consistency.ConfirmAsync(read, ct).ConfigureAwait(false);
            return confirmed.IsSuccess ? Result<SystemDescriptionView>.Success(read.View) : Result<SystemDescriptionView>.Failure(confirmed.Error!);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<SystemDescriptionView>.Failure(SystemDescriptionReadFailure.Of(failure));
        }
    }
}
