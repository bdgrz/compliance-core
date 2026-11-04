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
///     Maps a committed reviewer's explicit raise decision into findings. Failed or skipped attestations remain
///     facts for review; they do not decide whether a finding exists. Every emitted identity derives from the
///     exact committed review so replay never duplicates it.
/// </summary>
public sealed partial class ControlOccurrenceFindingReactor(IProjectionCheckpointStore checkpoints,
    IRequestBus bus, IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ControlOccurrence.Area), WorkloadName),
      IReactorHandler<ControlOccurrenceAttested>, IReactorHandler<ControlOccurrenceReviewed>
{
    public const string WorkloadName = "ControlOccurrenceFindingsV1";

    public async ValueTask<Result> HandleAsync(IReactorContext<ControlOccurrenceAttested> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context,
            new ControlOccurrence(ev.TenantId, ev.ProgramId, ev.ControlId, ev.OccurrenceId).Stream, ct).ConfigureAwait(false);
        _ = await ReadAsync<ControlAttestationView>(context, ev.AttestationContent, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IReactorContext<ControlOccurrenceReviewed> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context,
            new ControlOccurrence(ev.TenantId, ev.ProgramId, ev.ControlId, ev.OccurrenceId).Stream, ct).ConfigureAwait(false);
        var review = await ReadAsync<ControlOccurrenceReviewView>(context, ev.ReviewContent, ct)
            .ConfigureAwait(false);
        if (review.FindingDecision != "raise")
            return Result.Success;
        if (review.RequestedActions.Count == 0)
        {
            var rationale = string.IsNullOrWhiteSpace(review.FindingRationale) ? review.Rationale : review.FindingRationale;
            return await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
                Uuid.CreateVersion5(review.DecisionId, "finding"), ev.ControlId, ev.OccurrenceId,
                review.DecisionId.ToString(), "occurrence_review", rationale.Trim()), context, ct).ConfigureAwait(false);
        }
        for (var index = 0; index < review.RequestedActions.Count; index++)
        {
            var reactionOutcome1 = await bus.SendReactionAsync(new RaiseOccurrenceFinding(ev.TenantId, ev.ProgramId,
                Uuid.CreateVersion5(review.DecisionId, "requested-action-" +
                    index.ToString(CultureInfo.InvariantCulture)), ev.ControlId, ev.OccurrenceId,
                review.DecisionId.ToString(), "occurrence_review", review.RequestedActions[index]),
                context, ct).ConfigureAwait(false);
            if (!reactionOutcome1.IsSuccess)
                return reactionOutcome1;
        }

        return Result.Success;
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
