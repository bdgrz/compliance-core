using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Protects one identity slot of a prepared recovery. A competing association that is already definite rejects
///     the preparation; another recovery of the same account holding the slot is a wait; a slot whose protection for
///     this challenge was already settled, and a rejected preparation, make a delayed protection a terminal no-op.
/// </summary>
public sealed class ProtectRecoveryIdentityHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<ProtectRecoveryIdentity, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ProtectRecoveryIdentity> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyRecoveryFactAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        var slot = request.IdentityRole == RecoveryIdentityRole.Old ? frozen.OldIdentityId : frozen.ReplacementIdentityId;
        var stream = IdentityRecoveryReceiverSupport.IdentityStream(request.IdentityId);
        if (request.IdentityId != slot || request.Target != stream || request.AccountId != frozen.AccountId ||
            request.OldIdentityId != frozen.OldIdentityId || request.ReplacementIdentityId != frozen.ReplacementIdentityId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The command does not match the frozen candidate and identity role.");

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(ProtectRecoveryIdentity), stream,
            frozen, verified.Value!.EventId);
        return await support.ApplyAsync<UserIdentity, IdentityRecovery>(
            () => new UserIdentity(request.IdentityId, request.CommandId, nameof(ProtectRecoveryIdentity), request.ChallengeId),
            identity => identity.Acceptance, AcceptedOperationBinding.Of(provenance),
            async (_, cancel) => Result<IdentityRecovery>.Success(await support.Reader.HydrateAsync(
                IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId), cancel).ConfigureAwait(false)),
            (identity, recovery) =>
            {
                if (recovery.State == IdentityRecovery.Stage.Rejected)
                    return IdentityRecoveryReceiverSupport.Superseded("The preparation was rejected.");
                var decision = identity.DecideProtection(request.ChallengeId, request.AccountId, request.IdentityRole,
                    frozen.ReplacementIdentityId);
                return decision.Outcome switch
                {
                    UserIdentity.ProtectionOutcome.Proceed => null,
                    UserIdentity.ProtectionOutcome.Wait => IdentityRecoveryReceiverSupport.Wait(decision.Reason),
                    UserIdentity.ProtectionOutcome.Fenced => IdentityRecoveryReceiverSupport.Superseded(decision.Reason),
                    _ => recovery.State == IdentityRecovery.Stage.Prepared
                        ? IdentityRecoveryReceiverSupport.Rejected(decision.Reason)
                        : IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                            "An accepted swap cannot be rejected: " + decision.Reason),
                };
            },
            (identity, _) => identity.Protect(provenance, request.Intent, request.Source.RequiredFacts,
                request.ChallengeId, request.AccountId, request.IdentityRole, frozen.ReplacementIdentityId,
                request.Generation), context, ct).ConfigureAwait(false);
    }
}
