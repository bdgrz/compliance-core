using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class GetRiskMethodVersionHandler(RiskMethodReader reader)
    : IRequestHandler<GetRiskMethodVersion, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(
        IRequestContext<GetRiskMethodVersion> context, CancellationToken ct)
    {
        try
        {
            var method = await reader.ReadAsync(context.Request.TenantId, context.Request.ProgramId, ct).ConfigureAwait(false);
            return GetRiskMethodHandler.Found(method.GetVersion(context.Request.Version));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact risk method content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
