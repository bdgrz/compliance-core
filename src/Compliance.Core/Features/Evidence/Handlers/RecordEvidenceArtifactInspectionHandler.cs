using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Handlers;

public sealed class RecordEvidenceArtifactInspectionHandler(IAggregateExecutor executor)
    : IRequestHandler<RecordEvidenceArtifactInspection>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RecordEvidenceArtifactInspection> context,
        CancellationToken ct)
    {
        if (!RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Artifact inspection results can only be recorded by a trusted system process.")));

        var request = context.Request;
        return executor.ExecuteAsync(new EvidenceArtifact(request.TenantId, request.ArtifactId), artifact =>
            CommandFailureRequestAdapter.ToOutcome(artifact.RecordInspection(request.Outcome,
                request.InspectedAt)), context, ct);
    }
}
