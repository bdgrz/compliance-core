using System.Security.Claims;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Server-derived complete source population and physically verified bytes for a period close.</summary>
public interface ITypeIIPeriodCloseReconciliation
{
    ValueTask<Result<TypeIIPeriodCloseValidationContent>> ReadAsync(TypeIIPeriod period, ClaimsPrincipal actor,
        Uuid requesterUserId, OperationProvenance operation, CancellationToken ct);
}
