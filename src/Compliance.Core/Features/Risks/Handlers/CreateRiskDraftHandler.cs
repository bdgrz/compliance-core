using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Handlers;

public sealed class CreateRiskDraftHandler(RiskDraftOperations operations) : IRequestHandler<CreateRiskDraft, RiskRegistration>
{
    public ValueTask<Result<RiskRegistration>> HandleAsync(IRequestContext<CreateRiskDraft> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
