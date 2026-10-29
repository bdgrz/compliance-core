using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>
///     Starts <c>EngagementAcceptanceProcess</c> from a committed acceptance or staff assignment admission. The process
///     record, with its frozen intent and the canonical acquisition keys of its frozen manifests, exists before the
///     source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class EngagementAcceptanceReactor(IProjectionCheckpointStore checkpoints, ProcessRootReaction process,
    IImmutableContentStore content, ISourceFactResolver sources)
    : Reactor(checkpoints, EventStreamPattern.ForTenant(ServiceEngagementIndependence.Area), WorkloadName),
        IReactorHandler<ServiceEngagementAcceptanceRequested>,
        IReactorHandler<ServiceEngagementStaffAssignmentRequested>
{
    public const string WorkloadName = "EngagementAcceptanceV1";

    public ValueTask HandleAsync(IReactorContext<ServiceEngagementAcceptanceRequested> context, CancellationToken ct) =>
        VerifyAndStartAsync(context, context.Trigger, context.Trigger.Intent, ct);

    public ValueTask HandleAsync(IReactorContext<ServiceEngagementStaffAssignmentRequested> context,
        CancellationToken ct) => StartAsync(context, context.Trigger.Intent, ct);

    async ValueTask VerifyAndStartAsync<T>(IReactorContext context, T trigger, ImmutableContentRef intent, CancellationToken ct)
        where T : DomainEvent, Bdgrz.Compliance.Features.NativeOperations.Contracts.INativeOperationFact
    {
        await EngagementNativeSource.VerifyAsync(sources, content, context.Source.Stream,
            context.Source.ResourceOffset, trigger, ct).ConfigureAwait(false);
        await StartAsync(context, intent, ct).ConfigureAwait(false);
    }

    async ValueTask StartAsync(IReactorContext context, ImmutableContentRef intentReference, CancellationToken ct)
    {
        IReadOnlyList<string> targets;
        try
        {
            var realm = context.Source.Stream.Realm;
            var intent = await content.ReadJsonAsync<EngagementIndependenceIntent>(realm, intentReference, ct)
                .ConfigureAwait(false);
            targets = EngagementAcceptanceProcess.Targets(
                await content.ReadSelectionAsync<SourceUseRequirement>(realm, intent.SourceUses, ct)
                    .ConfigureAwait(false),
                await content.ReadSelectionAsync<PracticeSubjectIntent>(realm, intent.Subjects, ct)
                    .ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen acceptance cannot be read yet: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        _ = await process.StartAsync(EngagementAcceptanceEffects.ProcessName, context, intentReference, targets,
            $"reactor:{WorkloadName}", ct).ConfigureAwait(false);
    }
}
