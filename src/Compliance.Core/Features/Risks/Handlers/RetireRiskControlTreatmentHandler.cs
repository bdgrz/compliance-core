using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Retires only the exact accepted assertion; retries retain the original native result after succession.</summary>
public sealed class RetireRiskControlTreatmentHandler(NativeOperationExecutor operations, IAggregateReader reader,
    RiskGovernanceReader governanceReader, IImmutableContentStore content, TimeProvider clock) : IRequestHandler<RetireRiskControlTreatment>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RetireRiskControlTreatment> context, CancellationToken ct)
    {
        var request = context.Request;
        var retired = await RiskTreatmentAssertionOperations.ExecuteAsync<RetireRiskControlTreatment, UnitResult>(operations, reader,
            governanceReader, content, clock, context, request.TenantId, request.ProgramId, request.RiskId, request.CommandId,
            request.ExpectedRevision, null, request.TreatmentId, nameof(RetireRiskControlTreatment),
            async (owner, _, token) =>
            {
                if (owner.Accepted?.TreatmentId != request.TreatmentId)
                    return Result<PreparedRiskAssertion>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "Only the exact accepted control treatment assertion can be retired."));
                var gate = owner.PrepareRetire(request.TreatmentId, request.ExpectedRevision);
                if (!gate.IsSuccess)
                    return Result<PreparedRiskAssertion>.Failure(gate.Error!);
                if (RiskCommands.InvalidText(request.Rationale))
                    return Result<PreparedRiskAssertion>.Failure(new RequestError(RequestErrorKind.Validation,
                        "Retiring a control treatment requires a bounded rationale."));
                var frozen = await RiskCommands.FreezeUnitAsync(content, request.TenantId, token).ConfigureAwait(false);
                return Result<PreparedRiskAssertion>.Success(new(frozen.Response, frozen.Result));
            }, (owner, operation, first, frozen) => owner.Retire(operation, request.TreatmentId, request.Rationale.Trim(),
                operation.Actor, operation.AcceptedAt, first, frozen.Result), ct).ConfigureAwait(false);
        return retired.IsSuccess ? Result.Success : Result.Failure(retired.Error!);
    }
}
