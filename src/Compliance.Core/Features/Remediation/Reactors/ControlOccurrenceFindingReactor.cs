using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Remediation.Reactors;

/// <summary>
///     Routes control-operation exceptions into the shared finding path: a failed or skipped
///     attestation and each action a reviewer requests become a finding whose identity derives
///     from the exact attestation or decision, so replay never duplicates one. It reads the occurrence facts of the
///     tenant and their verified attestation and review content; the finding ids derive from those exact ids.
/// </summary>
public sealed partial class ControlOccurrenceFindingReactor(IProjectionCheckpointStore checkpoints,
    IRequestBus bus, IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ControlOccurrence.Area), WorkloadName),
      IReactorHandler<ControlOccurrenceAttested>, IReactorHandler<ControlOccurrenceReviewed>
{
    public const string WorkloadName = "ControlOccurrenceFindingsV1";

    public async ValueTask HandleAsync(IReactorContext<ControlOccurrenceAttested> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context,
            new ControlOccurrence(ev.TenantId, ev.ProgramId, ev.ControlId, ev.OccurrenceId).Stream, ct).ConfigureAwait(false);
        var attestation = await ReadAsync<ControlAttestationView>(context, ev.AttestationContent, ct)
            .ConfigureAwait(false);
        if (attestation.Result is not (ControlOperationStates.Failed or ControlOperationStates.Skipped))
            return;
        await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
            Uuid.CreateVersion5(attestation.AttestationId, "finding"), ev.ControlId,
            ev.OccurrenceId, attestation.AttestationId.ToString(), "control_occurrence",
            $"{attestation.Result}: {attestation.Rationale}"), context, ct).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(IReactorContext<ControlOccurrenceReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context,
            new ControlOccurrence(ev.TenantId, ev.ProgramId, ev.ControlId, ev.OccurrenceId).Stream, ct).ConfigureAwait(false);
        var review = await ReadAsync<ControlOccurrenceReviewView>(context, ev.ReviewContent, ct)
            .ConfigureAwait(false);
        if (review.Outcome != ControlOperationStates.ActionRequested)
            return;
        for (var index = 0; index < review.RequestedActions.Count; index++)
            await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
                Uuid.CreateVersion5(review.DecisionId, "requested-action-" +
                    index.ToString(CultureInfo.InvariantCulture)), ev.ControlId, ev.OccurrenceId,
                review.DecisionId.ToString(), "occurrence_review", review.RequestedActions[index]),
                context, ct).ConfigureAwait(false);
    }

    async ValueTask<T> ReadAsync<T>(IReactorContext context, ImmutableContentRef reference, CancellationToken ct)
        where T : notnull
    {
        try
        {
            return await content.ReadJsonAsync<T>(context.Source.Stream.Realm, reference, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The occurrence content cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }
    }
}
