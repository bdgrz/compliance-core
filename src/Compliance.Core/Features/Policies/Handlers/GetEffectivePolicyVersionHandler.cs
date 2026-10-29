using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Policies.Queries;
using Bdgrz.Compliance.Features.Policies.Readers;
namespace Bdgrz.Compliance.Features.Policies.Handlers;

public sealed class GetEffectivePolicyVersionHandler(PolicyReader reader)
    : IRequestHandler<GetEffectivePolicyVersion, PolicyVersionView>
{
    public async ValueTask<Result<PolicyVersionView>> HandleAsync(
        IRequestContext<GetEffectivePolicyVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        if (!policy.IsSuccess)
            return Result<PolicyVersionView>.Failure(policy.Error);
        return policy.Value.EffectiveVersion(request.EffectiveOn) is { } version
            ? Result<PolicyVersionView>.Success(version)
            : Result<PolicyVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "No policy version was effective on that date."));
    }
}
