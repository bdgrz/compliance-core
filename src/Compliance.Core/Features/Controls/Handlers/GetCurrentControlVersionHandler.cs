using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class GetCurrentControlVersionHandler(ControlActivationSource source)
    : IRequestHandler<GetCurrentControlVersion, ControlVersionView>
{
    public async ValueTask<Result<ControlVersionView>> HandleAsync(
        IRequestContext<GetCurrentControlVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Version(control.Value.ApprovedVersion,
                "The control has no approved version.")
            : Result<ControlVersionView>.Failure(control.Error);
    }
}
