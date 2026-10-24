using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Drives a completion review from its committed native root through the production process definition.</summary>
static class RiskActionProcesses
{
    public static async Task<ProcessRecord> RunAsync(IServiceProvider provider, Uuid tenantId, Uuid operationId,
        CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        NativeFactReference? source = null;
        RiskTreatmentActionCompletionReviewAccepted? accepted = null;
        await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), RiskTreatmentAction.Area),
                           EventCursor.Start, ct))
        {
            if (record.Event is not RiskTreatmentActionCompletionReviewAccepted fact ||
                fact.Operation.RootOperationId != operationId)
                continue;
            accepted = fact;
            source = NativeFactReference.Of(record.Stream, fact);
        }
        if (accepted is null || source is null)
            throw new InvalidOperationException("The review was not accepted.");
        var content = services.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<RiskActionCompletionReviewIntent>(tenantId.ToString(),
            accepted.Intent, ct);
        var decision = await content.ReadJsonAsync<RiskActionReviewDecision>(tenantId.ToString(),
            intent.AuthoredReview, ct);
        var cited = await ImmutableSelectionManifests.ReadAsync<ReviewedEvidenceRequest>(content, tenantId.ToString(),
            intent.CompletionEvidence, ct);
        var key = ProcessKey.ForRoot(RiskActionReviewEffects.ProcessName, operationId, source);
        await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(ProcessRecord.Start(key, source,
            accepted.Operation.Episode, accepted.Intent,
            cited.Select(item => RiskActionReviewEffects.EvidenceKey(tenantId, decision.ProgramId,
                item.EvidenceRequestId)), DateTimeOffset.UtcNow), ct);
        return await services.GetRequiredService<ProcessDriver>().RunAsync(key, "test-worker",
            new RequestDispatchContext(RequestActor.System), ct);
    }
}
