using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Handlers;

/// <summary>
///     Completes the original email challenge of a finished recovery. Only that challenge is completed, so a newer
///     challenge slot on the address is left exactly as it is.
/// </summary>
public sealed class CompleteEmailRecoveryChallengeHandler(IdentityRecoveryReceiverSupport support, TimeProvider clock)
    : IRequestHandler<CompleteEmailRecoveryChallenge, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteEmailRecoveryChallenge> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyRecoveryFactAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        if (request.Target.Realm != IdentityRecoveryReceiverSupport.Realm || request.Target.Area != "email-addresses" ||
            !Uuid.TryParse(request.Target.Resource, System.Globalization.CultureInfo.InvariantCulture, out var addressId))
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The command does not target an email address.");

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(CompleteEmailRecoveryChallenge),
            request.Target, frozen, verified.Value!.EventId);
        return await support.ApplyAsync(
            () => new EmailAddress(addressId, request.CommandId, nameof(CompleteEmailRecoveryChallenge), request.ChallengeId),
            address => address.Acceptance, AcceptedOperationBinding.Of(provenance),
            address => address.ClaimOf(request.ChallengeId) is { } claim && claim.OldIdentityId == frozen.OldIdentityId &&
                       claim.ReplacementIdentityId == frozen.ReplacementIdentityId && claim.Fact.UserId == frozen.AccountId
                ? null
                : IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The challenge was not claimed for this recovery."),
            (EmailAddress address) => address.CompleteIdentityRecovery(provenance, request.Intent, frozen.AccountId,
                request.ChallengeId, frozen.OldIdentityId, frozen.ReplacementIdentityId, clock.GetUtcNow()),
            context, ct).ConfigureAwait(false);
    }
}
