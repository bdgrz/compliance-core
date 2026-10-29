using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     Reads the complete service history and every materialized preview of a client from their owners. History is
///     never truncated; a preview that is admitted but not yet materialized is not listed until its process ends. The
///     reevaluation and partner lists belong to later owners and are empty here.
/// </summary>
public sealed class GetClientIndependenceHistoryHandler(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
    : IRequestHandler<GetClientIndependenceHistory, IndependenceHistoryView>
{
    public async ValueTask<Result<IndependenceHistoryView>> HandleAsync(
        IRequestContext<GetClientIndependenceHistory> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return Result<IndependenceHistoryView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Independence history requires its owning client tenant."));
        try
        {
            return await ReadAsync(request, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<IndependenceHistoryView>.Failure(IndependenceCatalogContent.Failure(error));
        }
    }

    async Task<Result<IndependenceHistoryView>> ReadAsync(GetClientIndependenceHistory request, CancellationToken ct)
    {
        var realm = IndependenceContent.Realm(request.TenantId);
        var owner = await reader.HydrateAsync(new ClientServiceIndependence(request.TenantId), ct)
            .ConfigureAwait(false);
        var history = await ClientServiceNativeHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        var services = await ClientServiceHistory.ReadAsync(events, content, owner, ct).ConfigureAwait(false);
        var evaluations = new List<IndependenceEvaluationView>(history.Admissions.Count);
        foreach (var admitted in history.Admissions)
        {
            var decision = await reader.HydrateAsync(new ClientIndependenceDecision(request.TenantId,
                admitted.Operation.LogicalId), ct).ConfigureAwait(false);
            if (decision.Evaluated is { } evaluated)
            {
                var source = admitted.Operation;
                var child = evaluated.Operation;
                if (evaluated.Intent != admitted.Intent || child.RootOperationId != source.RootOperationId ||
                    child.CausationEventId != admitted.Metadata.EventId || child.Actor != source.Actor ||
                    child.AuthoredIntentSha256 != source.AuthoredIntentSha256 || child.FirstOwner != owner.Stream ||
                    child.OriginalExpectedRevision != source.OriginalExpectedRevision || child.Episode != source.Episode)
                    throw new ImmutableContentIntegrityException("The preview child does not materialize its exact original client admission.");
                var view = await content.ReadJsonAsync<IndependenceEvaluationView>(realm, evaluated.EvaluationContent, ct)
                    .ConfigureAwait(false);
                var result = await content.ReadJsonAsync<MutationResultContent>(realm, evaluated.OperationResult!, ct).ConfigureAwait(false);
                if (view.ClientTenantId != request.TenantId || view.EvaluationId != source.LogicalId ||
                    view.Actor != source.Actor || view.RecordedAt != source.AcceptedAt || !view.ProductionAcceptanceBlocked ||
                    result.Schema != IndependenceContent.EvaluationSchema || result.Value is not StandardResponseBody response ||
                    response.Content != evaluated.EvaluationContent)
                    throw new ImmutableContentIntegrityException("The preview result differs from its exact original typed body and frozen decision instant.");
                evaluations.Add(view);
            }
        }

        return Result<IndependenceHistoryView>.Success(new IndependenceHistoryView(request.TenantId, (long)owner.Epoch,
            Array.AsReadOnly(evaluations.Select(evaluation => evaluation.EvaluatedRules)
                .DistinctBy(version => version.Version).OrderBy(version => version.Version).ToArray()),
            Array.AsReadOnly(services.ToArray()), Array.AsReadOnly(evaluations.ToArray())));
    }
}
