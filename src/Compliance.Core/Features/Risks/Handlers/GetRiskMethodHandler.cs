using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Reads the small per-program method stream directly, so reads never lag writes.</summary>
public sealed class GetRiskMethodHandler(RiskMethodReader reader)
    : IRequestHandler<GetRiskMethod, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(
        IRequestContext<GetRiskMethod> context, CancellationToken ct)
    {
        try
        {
            var version = await reader.CurrentAsync(context.Request.TenantId, context.Request.ProgramId, ct).ConfigureAwait(false);
            return Found(version);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact risk method content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }

    internal static Result<RiskMethodVersionView> Found(RiskMethodVersionView? version) =>
        version is null
            ? Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk method version was not found."))
            : Result<RiskMethodVersionView>.Success(version);
}
