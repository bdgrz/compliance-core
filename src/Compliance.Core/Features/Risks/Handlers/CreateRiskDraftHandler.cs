using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class CreateRiskDraftHandler(RiskDraftOperations operations) : IRequestHandler<CreateRiskDraft, RiskRegistration>
{
    public ValueTask<Result<RiskRegistration>> HandleAsync(IRequestContext<CreateRiskDraft> context, CancellationToken ct) =>
        operations.CreateAsync(context, ct);
}
