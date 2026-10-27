using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class GetControlVersionHandler(ControlActivationSource source)
    : IRequestHandler<GetControlVersion, ControlVersionView>
{
    public async ValueTask<Result<ControlVersionView>> HandleAsync(
        IRequestContext<GetControlVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Version(control.Value.ReadVersions()
                    .SingleOrDefault(version => version.VersionId == request.VersionId),
                "The approved control version was not found.")
            : Result<ControlVersionView>.Failure(control.Error);
    }
}
