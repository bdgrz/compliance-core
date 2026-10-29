using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Commands;

[Discriminator("bdgrz.risk.method.publish", 1)]
public sealed record PublishRiskMethodVersion(Uuid TenantId, Uuid ProgramId,
    long ExpectedVersion, IReadOnlyList<string> LikelihoodScale,
    IReadOnlyList<string> ImpactScale, int? AppetiteThreshold = null)
    : IRequest<RiskMethodVersionView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable, IIdentifiedRequest
{
    // One publication can occupy an original version slot. Scope and original gate form its retry identity.
    Uuid IIdentifiedRequest.RequestId => Uuid.CreateVersion5(
        Uuid.CreateVersion5(TenantId, $"risk-method:{ProgramId}"),
        "publish-version:" + ExpectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
