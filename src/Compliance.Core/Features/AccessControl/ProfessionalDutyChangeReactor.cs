using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
