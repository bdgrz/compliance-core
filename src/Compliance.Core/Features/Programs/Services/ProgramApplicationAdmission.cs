using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Services;

/// <summary>Checks current source authority before work is admitted to an existing program.</summary>
/// <remarks>
/// The approved Program lifecycle has no retirement, suspension or ending transition: the same
/// program continues through readiness, Type I and Type II. Active therefore requires its exact
/// committed creation and an active tenant. Discovery projections never establish this authority.
/// Call this check for every admission, including a request whose prior attempt was accepted.
/// </remarks>
public sealed class ProgramApplicationAdmission(IAggregateReader reader, ITenantActivity tenants)
{
    public async ValueTask<Result> CheckAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Program admission requires exact tenant and program identities."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Program admission requires an active tenant."));
        var program = await reader.HydrateAsync(new ComplianceProgram(tenantId, programId), ct)
            .ConfigureAwait(false);
        return program.IsCreated && program.TenantId == tenantId && program.Id == programId
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The program was not found in its authoritative source."));
    }
}
