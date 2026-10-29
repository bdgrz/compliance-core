using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

/// <summary>
///     Starts <c>ProfessionalDutyChangeProcess</c> from a committed change root fact. The process record, with its
///     frozen target, exists before the source checkpoint advances; the sweep resumes it if this reactor stops.
/// </summary>
sealed partial class ProfessionalDutyChangeReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content, IRequestBus bus, ISourceFactResolver sources,
    ProfessionalDutyChangeVerifier verifier)
    : Reactor(checkpoints, EventStreamPattern.ForPattern(ProfessionalDutyContent.Realm,
            FirmProfessionalDutyDesignation.Area), WorkloadName),
        IReactorHandler<ProfessionalDutyChangeAccepted>
{
    public const string WorkloadName = "ProfessionalDutyChangeV1";

    public async ValueTask HandleAsync(IReactorContext<ProfessionalDutyChangeAccepted> context, CancellationToken ct)
    {
        if (!Uuid.TryParse(context.Source.Stream.Resource, null, out var id) ||
            context.Trigger.Metadata is null || context.Trigger.Metadata.AggregateId != id ||
            context.Trigger.Operation.Owner != context.Source.Stream)
            throw new SourceFactVerificationException("A designation reaction requires its exact native owner checkpoint.");
        var (_, actual, _) = await SourceFactProofs.BuildAsync<ProfessionalDutyChangeAccepted>(sources, content,
            ProfessionalDutyContent.Realm, NativeFactReference.Of(context.Source.Stream, context.Trigger),
            context.Source.ResourceOffset, ct).ConfigureAwait(false);
        if (actual.Metadata != context.Trigger.Metadata || ContentDigest.Sha256(actual, typeof(ProfessionalDutyChangeAccepted)) !=
            ContentDigest.Sha256(context.Trigger, typeof(ProfessionalDutyChangeAccepted)))
            throw new SourceFactVerificationException("The designation trigger differs from its actual committed payload and complete metadata.");
        var verified = await verifier.VerifyAsync(NativeFactReference.Of(context.Source.Stream, context.Trigger),
            context.Trigger.Intent, context.Trigger.Operation.RootOperationId, context.Trigger.Operation.LogicalId, ct)
            .ConfigureAwait(false);
        if (verified.Error is not null)
            throw new ReactionCommandFailedException(verified.Error);
        ProfessionalDutyChangeIntent intent;
        VerifiedFirmProfessionalDutyAssertion evidence;
        try
        {
            intent = await ProfessionalDutyContent.ReadIntentAsync(content, context.Trigger.Intent, ct)
                .ConfigureAwait(false);
            evidence = await ProfessionalDutyContent.ReadVerifiedScopeAsync(content, intent.VerifiedScope, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new ReactionCommandFailedException(new RequestError(RequestErrorKind.Conflict,
                $"The frozen change cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException));
        }

        var record = await process.StartAsync(ProfessionalDutyEffects.ProcessName, context, context.Trigger.Intent,
            [ProfessionalDutyTarget.For(intent, evidence).ToString()], $"reactor:{WorkloadName}", ct)
            .ConfigureAwait(false);
        // A process is keyed by the signed assertion identity. A record that belongs to another accepted change of
        // the same identity means this change can never be claimed: reject it so it does not stay pending.
        if (record.Source.EventId == context.Trigger.Metadata.EventId)
            return;
        var rejected = await bus.SendAsync(new RejectProfessionalDutyChange(ProfessionalDutyEffects.ReceiverCommandId(
            context.Trigger.Metadata.EventId, nameof(RejectProfessionalDutyChange), null, intent.Header.Episode),
            intent.VerifiedAssertionId, context.Source.Stream, ProfessionalDutyEffects.Generation, 0,
            context.Trigger.Intent), context, ct).ConfigureAwait(false);
        if (!rejected.IsSuccess)
            throw new ReactionCommandFailedException(rejected.Error!);
    }
}
