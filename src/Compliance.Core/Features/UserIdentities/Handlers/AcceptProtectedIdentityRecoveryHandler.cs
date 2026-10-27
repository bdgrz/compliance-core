using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Accepts the swap only now: both identity slots carry the exact protection proofs and are still protected.
///     A rejected preparation or a settled slot makes a delayed acceptance a terminal no-op.
/// </summary>
public sealed class AcceptProtectedIdentityRecoveryHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<AcceptProtectedIdentityRecovery, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<AcceptProtectedIdentityRecovery> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyRecoveryFactAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        var target = IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId).Stream;
        if (request.Target != target)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The command does not target this recovery.");
        var proofs = await support.VerifyProtectionAsync(request.ProtectionProofs, request, frozen, ct)
            .ConfigureAwait(false);
        if (!proofs.IsSuccess)
            return Result<ReceiverOutcome>.Failure(proofs.Error!);

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(AcceptProtectedIdentityRecovery),
            target, frozen, verified.Value!.EventId);
        return await support.ApplyAsync<IdentityRecovery, ProtectedSlots>(
            () => IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId, request.CommandId),
            current => current.Acceptance, AcceptedOperationBinding.Of(provenance),
            async (_, cancel) =>
            {
                var old = await support.Reader.HydrateAsync(new UserIdentity(frozen.OldIdentityId, request.ChallengeId), cancel)
                    .ConfigureAwait(false);
                var replacement = await support.Reader.HydrateAsync(new UserIdentity(frozen.ReplacementIdentityId, request.ChallengeId), cancel)
                    .ConfigureAwait(false);
                return Result<ProtectedSlots>.Success(new ProtectedSlots(old, replacement));
            },
            (current, slots) =>
            {
                if (current.State == IdentityRecovery.Stage.Rejected || slots.Old.IsSettledFor(request.ChallengeId) ||
                    slots.Replacement.IsSettledFor(request.ChallengeId))
                    return IdentityRecoveryReceiverSupport.Superseded("The preparation was rejected or its protection settled.");
                return slots.Old.IsProtectedFor(request.ChallengeId) && slots.Replacement.IsProtectedFor(request.ChallengeId)
                    ? null
                    : IdentityRecoveryReceiverSupport.Wait("A protected slot is not readable yet.");
            },
            (current, _) => current.Accept(provenance, request.Intent, request.ProtectionProofs),
            context, ct).ConfigureAwait(false);
    }

    sealed record ProtectedSlots(UserIdentity Old, UserIdentity Replacement);
}
