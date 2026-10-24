using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Reads the complete service history and every materialized preview of a client from their owners. History is
///     never truncated; a preview that is admitted but not yet materialized is not listed until its process ends. The
///     reevaluation and partner lists belong to later owners and are empty here.
/// </summary>
public sealed class GetClientIndependenceHistoryHandler(IAggregateReader reader, IImmutableContentStore content)
    : IRequestHandler<GetClientIndependenceHistory, IndependenceHistoryView>
{
    public async ValueTask<Result<IndependenceHistoryView>> HandleAsync(
        IRequestContext<GetClientIndependenceHistory> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return Result<IndependenceHistoryView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Independence history requires its owning client tenant."));
        var realm = IndependenceContent.Realm(request.TenantId);
        var owner = await reader.HydrateAsync(new ClientServiceIndependence(request.TenantId), ct)
            .ConfigureAwait(false);
        var services = new List<NonattestServiceView>(owner.Services.Count);
        foreach (var recorded in owner.Services)
            services.Add(await content.ReadJsonAsync<NonattestServiceView>(realm, recorded.ServiceContent, ct)
                .ConfigureAwait(false));
        var evaluations = new List<IndependenceEvaluationView>(owner.Admissions.Count);
        foreach (var admitted in owner.Admissions)
        {
            var decision = await reader.HydrateAsync(new ClientIndependenceDecision(request.TenantId,
                admitted.Operation.LogicalId), ct).ConfigureAwait(false);
            if (decision.Evaluated is { } evaluated)
                evaluations.Add(await content.ReadJsonAsync<IndependenceEvaluationView>(realm,
                    evaluated.EvaluationContent, ct).ConfigureAwait(false));
        }

        return Result<IndependenceHistoryView>.Success(new IndependenceHistoryView(request.TenantId, (long)owner.Epoch,
            Array.AsReadOnly(evaluations.Select(evaluation => evaluation.EvaluatedRules)
                .DistinctBy(version => version.Version).OrderBy(version => version.Version).ToArray()),
            Array.AsReadOnly(services.ToArray()), Array.AsReadOnly(evaluations.ToArray())));
    }
}
