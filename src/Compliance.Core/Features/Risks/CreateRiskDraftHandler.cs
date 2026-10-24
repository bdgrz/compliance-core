using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class CreateRiskDraftHandler(IAggregateExecutor executor, IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CreateRiskDraft, RiskRegistration>
{
    public async ValueTask<Result<RiskRegistration>> HandleAsync(IRequestContext<CreateRiskDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var identifier = RiskDraft.NormalizeIdentifier(request.Identifier);
        if (request.RiskId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || identifier.Length is < 1 or > 80 ||
            request.RiskId != RiskDraft.IdFor(request.TenantId, request.ProgramId, identifier))
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "An authored risk id must equal the documented UUIDv5 identity of its tenant, program and normalized identifier."));
        var user = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var actor = RbacIds.Member(request.TenantId, user);
        var current = await reader.HydrateAsync(new RiskDraft(request.TenantId, request.RiskId), ct).ConfigureAwait(false);
        if (current.ReplayCreate(request, actor) is { } original)
            return original;
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
        return await executor.ExecuteAsync(new RiskDraft(request.TenantId, request.RiskId),
            risk => AggregateOutcome.CommitOnSuccess(risk.Create(request, actor,
                UserIdentityClaims.BdgrzDisplay(context.Actor, user), clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
