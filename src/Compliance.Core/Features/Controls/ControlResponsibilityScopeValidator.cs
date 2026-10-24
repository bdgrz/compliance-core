using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

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
