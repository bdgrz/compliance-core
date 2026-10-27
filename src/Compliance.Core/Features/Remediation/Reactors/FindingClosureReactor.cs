using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Reactors;

/// <summary>
///     Starts <c>FindingClosureProcess</c> from a committed closure acceptance. The process record, with the complete
///     canonical action key list of the frozen manifest, exists before the source checkpoint advances; the sweep resumes
///     it if this reactor stops. A manifest that cannot be read yet is transient: no record is created from an
///     unverified closure.
/// </summary>
sealed partial class FindingClosureReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(FindingAggregate.Area), WorkloadName),
        IReactorHandler<FindingClosureAccepted>
{
    public const string WorkloadName = "FindingClosureV1";

    public async ValueTask HandleAsync(IReactorContext<FindingClosureAccepted> context, CancellationToken ct)
    {
        var accepted = context.Trigger;
        await FindingReactorSources.VerifyAsync(sources, content, Pattern, context, accepted.Owner, ct)
            .ConfigureAwait(false);
        var tenant = Uuid.Parse(context.Source.Stream.Realm, System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<AdmittedChild> children;
        ClosureIntent intent;
        ClosureDecision decision;
        try
        {
            intent = await FindingContent.ReadAsync<ClosureIntent>(content, tenant, accepted.Intent, ct)
                .ConfigureAwait(false);
            decision = await FindingContent.ReadAsync<ClosureDecision>(content, tenant, intent.AuthoredDecision, ct)
                .ConfigureAwait(false);
            children = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(content, tenant.ToString(),
                intent.AdmittedChildren, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen closure cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(FindingClosureEffects.ProcessName, context, accepted.Intent,
            children.Select(child => FindingClosureEffects.ActionKey(tenant, decision.ProgramId, intent.ParentId,
                child.ChildId)), $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
