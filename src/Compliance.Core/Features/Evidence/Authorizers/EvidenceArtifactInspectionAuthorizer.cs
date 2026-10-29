using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Authorizers;

sealed class EvidenceArtifactInspectionAuthorizer
    : IRequestAuthorizer<RecordEvidenceArtifactInspection>
{
    public ValueTask<Result> AuthorizeAsync(IRequestContext<RecordEvidenceArtifactInspection> context,
        CancellationToken ct) => ValueTask.FromResult(RequestActor.IsSystem(context.Actor)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
            "Artifact inspection results can only be recorded by a trusted system process.")));
}
