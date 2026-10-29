using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

/// <summary>
///     Closes an observation that currently exists for the tenant: a projected joiner, mover, or
///     leaver, or a reconciliation finding evaluated now. An already-closed observation may be
///     retried with the same closure even after its facts changed. Closure never changes access.
/// </summary>
public sealed class ResolveWorkforceObservationHandler(IAggregateExecutor executor,
    IAggregateReader reader, IWorkforceObservationDirectoryReader observations,
    WorkforceRosterReconciler reconciler, TimeProvider clock)
    : IRequestHandler<ResolveWorkforceObservation>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ResolveWorkforceObservation> context,
        CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Workforce decision requires a personal HTTP invocation."));
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var existing = await reader.HydrateAsync(
            new WorkforceObservationResolution(request.TenantId, request.ObservationId), ct)
            .ConfigureAwait(false);
        if (!existing.IsResolved)
        {
            var stored = await observations.GetAsync(request.TenantId, request.ObservationId, ct)
                .ConfigureAwait(false);
            if (stored is null || stored.TenantId != request.TenantId)
            {
                var evaluated = await reconciler.EvaluateAsync(request.TenantId, ct)
                    .ConfigureAwait(false);
                if (!evaluated.IsSuccess)
                    return Result.Failure(evaluated.Error);
                if (evaluated.Value.All(item => item.ObservationId != request.ObservationId))
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The workforce observation was not found."));
            }
        }
        return await executor.ExecuteAsync(
            new WorkforceObservationResolution(request.TenantId, request.ObservationId),
            resolution => CommandFailureRequestAdapter.ToOutcome(resolution.Resolve(
                request.Resolution, request.Note, actor, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
