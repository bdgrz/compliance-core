using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Reactors;

/// <summary>
///     Starts the email challenge delivery process when a verification or identity recovery challenge is accepted on an
///     email address. The process record, with the effect payload frozen from the accepted fact, exists before the
///     source checkpoint advances; delivery itself, and every retry of it, belongs to the process.
/// </summary>
public sealed partial class EmailChallengeDeliveryReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content, TimeProvider clock)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "email-addresses")),
      IReactorHandler<EmailChallengeIssued>,
      IReactorHandler<IdentityRecoveryChallengeIssued>
{
    public ValueTask HandleAsync(IReactorContext<EmailChallengeIssued> context, CancellationToken ct)
    {
        var issued = context.Trigger;
        return StartAsync(context, issued.Operation, issued.ChallengeId, issued.UserId, issued.ExpiresAt,
            issued.TokenKeyId, EmailChallengeDeliveryIntent.Verification, ct);
    }

    public ValueTask HandleAsync(IReactorContext<IdentityRecoveryChallengeIssued> context, CancellationToken ct)
    {
        var issued = context.Trigger;
        return StartAsync(context, issued.Operation, issued.ChallengeId, issued.UserId, issued.ExpiresAt,
            issued.TokenKeyId, EmailChallengeDeliveryIntent.Recovery, ct);
    }

    async ValueTask StartAsync(IReactorContext context, OperationProvenance operation, Uuid challengeId, Uuid userId,
        DateTimeOffset expiresAt, string tokenKeyId, string purpose, CancellationToken ct)
    {
        // A challenge that expired before this reaction can never be used, so nothing is delivered for it.
        if (clock.GetUtcNow() >= expiresAt)
            return;
        var intent = new EmailChallengeDeliveryIntent(new DurableIntentHeader(operation.RootOperationId,
            operation.Owner, operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor,
            operation.Episode, operation.OriginalExpectedRevision, operation.AuthoredIntentSha256,
            operation.AcceptedAt), challengeId, userId, purpose, expiresAt, tokenKeyId);
        // The content id derives from the source fact, so a replayed reaction freezes the identical intent.
        var frozen = await content.WriteJsonAsync(EmailChallengeDeliveryWorker.Realm,
            Uuid.CreateVersion5(context.Source.Event.Metadata.EventId, EmailChallengeDeliveryIntent.Schema),
            EmailChallengeDeliveryIntent.Schema, intent, ct).ConfigureAwait(false);
        await process.StartAsync(EmailChallengeDeliveryProcess.Name, context, frozen, [],
            nameof(EmailChallengeDeliveryReactor), ct).ConfigureAwait(false);
    }
}
