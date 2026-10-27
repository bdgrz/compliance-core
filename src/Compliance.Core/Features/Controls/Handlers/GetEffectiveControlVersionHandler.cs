using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Queries;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class GetEffectiveControlVersionHandler(ControlActivationSource source)
    : IRequestHandler<GetEffectiveControlVersion, ControlVersionView>
{
    public async ValueTask<Result<ControlVersionView>> HandleAsync(
        IRequestContext<GetEffectiveControlVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.EffectiveOn == default)
            return Result<ControlVersionView>.Failure(new RequestError(
                RequestErrorKind.Validation, "An effective date is required."));
        var control = await source.LoadAsync(request.TenantId, request.ProgramId,
            request.ControlId, ct).ConfigureAwait(false);
        return control.IsSuccess
            ? ControlActivationSource.Version(control.Value.EffectiveVersion(request.EffectiveOn),
                "No approved control version is effective on that date.")
            : Result<ControlVersionView>.Failure(control.Error);
    }
}
