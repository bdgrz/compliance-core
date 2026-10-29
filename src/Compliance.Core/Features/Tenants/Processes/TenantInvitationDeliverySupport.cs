using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Tenants.Processes;

/// <summary>
///     What the receivers of the invitation delivery process share: verifying the invitation fact behind a command
///     and advancing the delivery record by compare-and-swap.
/// </summary>
public sealed class TenantInvitationDeliverySupport(ISourceFactResolver resolver, ITenantInvitationDeliveryStore deliveries)
{
    const int MaximumAttempts = 8;

    /// <summary>The delivery records the receivers advance.</summary>
    public ITenantInvitationDeliveryStore Deliveries => deliveries;

    /// <summary>The permanent error for a proof or command that does not match the invitation.</summary>
    public static Result<ReceiverOutcome> Invalid(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    /// <summary>A terminal no-op because a proven later fact makes the step moot.</summary>
    public static Result<ReceiverOutcome> Superseded(string reason) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(reason, null));

    /// <summary>The reference to the invitation fact a verified proof names.</summary>
    public static NativeFactReference Reference(VerifiedSourceProof<TenantMemberInvited> verified) =>
        new(verified.Source, verified.EventId, verified.OwnerRevision);

    /// <summary>Re-reads the exact invitation fact; every part of the command must agree with it.</summary>
    public async Task<Result<VerifiedSourceProof<TenantMemberInvited>>> VerifyAsync(
        Bdgrz.Compliance.Features.Proofs.Models.SourceFactProof<TenantMemberInvited> proof,
        ITenantInvitationDeliveryReceiver request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proof);
        VerifiedSourceProof<TenantMemberInvited> verified;
        try
        {
            verified = await resolver.VerifyAsync(proof, ct).ConfigureAwait(false);
        }
        catch (SourceFactVerificationException exception)
        {
            return Result<VerifiedSourceProof<TenantMemberInvited>>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The source proof does not verify: {exception.Message}", false));
        }

        var fact = verified.Facts;
        return fact.TenantId != request.TenantId || fact.InvitationId != request.InvitationId ||
               verified.Source.Realm != request.TenantId.ToString()
            ? Result<VerifiedSourceProof<TenantMemberInvited>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The invitation fact does not match the delivery command.", false))
            : Result<VerifiedSourceProof<TenantMemberInvited>>.Success(verified);
    }

    /// <summary>Applies <paramref name="change" /> to the current record and stores it, rereading on a lost compare-and-swap.</summary>
    public async Task<TenantInvitationDeliveryRecord> UpdateAsync(Uuid tenantId, Uuid invitationId,
        Func<TenantInvitationDeliveryRecord, TenantInvitationDeliveryRecord> change, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var current = await deliveries.CreateIfAbsentAsync(TenantInvitationDeliveryRecord.Start(tenantId, invitationId),
                ct).ConfigureAwait(false);
            var next = change(current);
            if (ReferenceEquals(next, current))
                return current;
            try
            {
                return await deliveries.ReplaceAsync(next, current.Version, ct).ConfigureAwait(false);
            }
            catch (ProcessConcurrencyException) when (attempt < MaximumAttempts)
            {
                // Another receiver advanced the record; decide again against its result.
            }
        }
    }
}
