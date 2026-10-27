using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The email challenge delivery process. Its root is the accepted verification or identity recovery challenge on
///     an email address. One stage dispatches the receiver of the challenge's purpose; the other is skipped. Every
///     attempt is a typed outcome command on the delivery ledger, so no delivery receipt ever becomes a fact of the
///     address stream, and a transient or ambiguous result is retried by the process record's own due time within the
///     attempt budget. The receivers verify their source fact by hydrating the address, so the process needs no
///     broker read of its own.
/// </summary>
public static class EmailChallengeDeliveryProcess
{
    /// <summary>The catalog name of the process.</summary>
    public const string Name = "EmailChallengeDeliveryProcess";

    /// <summary>The registered definition; it equals the catalog entry exactly.</summary>
    public static ProcessDefinition Definition { get; } = new(Name,
        [nameof(EmailChallengeIssued), nameof(IdentityRecoveryChallengeIssued)],
        true,
        [
            new ProcessStageDefinition("deliver",
            [
                EmailChallengeDeliveryReceivers.DeliverEmailChallenge,
                EmailChallengeDeliveryReceivers.DeliverRecoveryChallenge,
            ], "At-least-once; an unknown result is reconciled before the same message is resent."),
        ],
        [],
        CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var content = services.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<EmailChallengeDeliveryIntent>(step.Record.Key.Realm, step.Record.Intent, ct)
            .ConfigureAwait(false);
        var challenge = intent.ChallengeId;
        var target = EmailChallengeDeliveryWorker.DeliveryStream(challenge);
        return step.Receiver switch
        {
            EmailChallengeDeliveryReceivers.DeliverEmailChallenge when intent.Purpose == EmailChallengeDeliveryIntent.Verification =>
                new DeliverEmailChallenge(step.CommandId, step.Record.Source, target, challenge,
                    EmailChallengeDeliveryWorker.FrozenGeneration, null, step.Record.Intent, challenge),
            EmailChallengeDeliveryReceivers.DeliverRecoveryChallenge when intent.Purpose == EmailChallengeDeliveryIntent.Recovery =>
                new DeliverRecoveryChallenge(step.CommandId, step.Record.Source, target, challenge,
                    EmailChallengeDeliveryWorker.FrozenGeneration, null, step.Record.Intent, challenge),
            _ => null,
        };
    }
}
