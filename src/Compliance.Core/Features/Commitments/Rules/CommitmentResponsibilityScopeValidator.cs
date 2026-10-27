using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
namespace Bdgrz.Compliance.Features.Commitments.Rules;

sealed class CommitmentResponsibilityScopeValidator(CommitmentReader commitments)
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
        CancellationToken ct = default)
    {
        try
        {
            var draft = await commitments.ReadAsync(tenantId, scope.RecordId, ct).ConfigureAwait(false);
            if (!draft.IsCreated)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The responsibility source record was not found."));
            return draft.IsCurrentResponsibilityScope(scope)
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Responsibilities must target the current exact pending commitment revision."));
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result.Failure(CommitmentReadFailure.Of(failure));
        }
    }
}
