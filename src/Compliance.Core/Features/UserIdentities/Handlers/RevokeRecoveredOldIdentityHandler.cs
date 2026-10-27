using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Revokes the old identity to the replacement, and only with the authoritative registration proof of that
///     replacement. An old identity already revoked to the same replacement returns the existing revocation without
///     appending; one revoked to another identity is a permanent conflict.
/// </summary>
public sealed class RevokeRecoveredOldIdentityHandler(IdentityRecoveryReceiverSupport support, TimeProvider clock)
    : IRequestHandler<RevokeRecoveredOldIdentity, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RevokeRecoveredOldIdentity> context,
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
        var stream = IdentityRecoveryReceiverSupport.IdentityStream(frozen.OldIdentityId);
        if (request.Target != stream || request.OldIdentityId != frozen.OldIdentityId ||
            request.ReplacementIdentityId != frozen.ReplacementIdentityId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The command does not match the frozen identities.");
        var registration = await support.VerifySourceAsync(request.ReplacementRegistrationProof, ct).ConfigureAwait(false);
        if (!registration.IsSuccess)
            return Result<ReceiverOutcome>.Failure(registration.Error!);
        var registered = registration.Value!;
        if (registered.Source != IdentityRecoveryReceiverSupport.IdentityStream(frozen.ReplacementIdentityId) ||
            registered.Facts.UserId != frozen.AccountId || registered.Facts.Provider != frozen.ReplacementProvider ||
            registered.Facts.Identifier != frozen.ReplacementSubject)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The registration proof is not the replacement identity of this account.");

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(RevokeRecoveredOldIdentity), stream,
            frozen, verified.Value!.EventId);
        return await support.ApplyAsync(
            () => new UserIdentity(frozen.OldIdentityId, request.CommandId, nameof(RevokeRecoveredOldIdentity), request.ChallengeId),
            identity => identity.Acceptance, AcceptedOperationBinding.Of(provenance),
            identity =>
            {
                if (!identity.IsRegistered || identity.UserId != frozen.AccountId)
                    return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                        "The old identity is not linked to the recovering account.");
                if (!identity.IsRevoked)
                    return null;
                return identity.ReplacementIdentityId == frozen.ReplacementIdentityId
                    ? Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(NativeFactReference.Of(stream,
                        identity.RevocationFact!)))
                    : IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                        "The old identity was revoked in favor of another identity.");
            },
            (UserIdentity identity) => identity.RevokeRecovered(provenance, request.Intent, request.ChallengeId,
                frozen.ReplacementIdentityId, clock.GetUtcNow()), context, ct).ConfigureAwait(false);
    }
}
