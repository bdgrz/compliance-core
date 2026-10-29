using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Controls.Rules;

sealed class ControlResponsibilityScopeValidator(ControlReader controlSources)
{
    public ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default) =>
        ControlSourceFailures.ClassifyAsync(() => ValidateCoreAsync(tenantId, scope, ct));

    async ValueTask<Result> ValidateCoreAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default)
    {
        var control = await controlSources.ReadAsync(tenantId, scope.RecordId, ct)
            .ConfigureAwait(false);
        if (!control.IsVisible)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The responsibility source record was not found."));
        return control.IsCurrentResponsibilityScope(scope)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Responsibilities must target the current exact control draft revision."));
    }
}
