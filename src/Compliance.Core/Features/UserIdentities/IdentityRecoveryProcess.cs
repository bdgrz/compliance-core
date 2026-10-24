using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     The ordered identity recovery process. Its root is the claim of the recovery challenge on the email address.
///     Both identity slots are protected in canonical key order before the swap is accepted; an accepted swap
///     registers the replacement, revokes the old identity and completes forward, and the settle stage releases
///     the protections, completes the original email challenge and sends the independent notice. A definite
///     conflict before acceptance rejects the preparation through the abort receiver and settles the same
///     protections so nothing stays held.
/// </summary>
public static class IdentityRecoveryProcess
{
    /// <summary>The catalog name of the process.</summary>
    public const string Name = "IdentityRecoveryProcess";

    /// <summary>The registered definition; it equals the catalog entry exactly.</summary>
    public static ProcessDefinition Definition { get; } = new(Name,
        [nameof(IdentityRecoveryChallengeClaimed)],
        true,
        [
            new ProcessStageDefinition("prepare", [IdentityRecoveryReceivers.MaterializeIdentityRecoveryPreparation]),
            new ProcessStageDefinition("protect", [IdentityRecoveryReceivers.ProtectRecoveryIdentity],
                "Old and replacement slots in canonical key order."),
            new ProcessStageDefinition("accept", [IdentityRecoveryReceivers.AcceptProtectedIdentityRecovery],
                "Irreversible; no cancellation afterwards."),
            new ProcessStageDefinition("register", [IdentityRecoveryReceivers.RegisterRecoveredIdentity]),
            new ProcessStageDefinition("revoke", [IdentityRecoveryReceivers.RevokeRecoveredOldIdentity],
                "Only with authoritative replacement proof."),
            new ProcessStageDefinition("complete", [IdentityRecoveryReceivers.CompleteAcceptedIdentityRecovery]),
            new ProcessStageDefinition("settle",
            [
                IdentityRecoveryReceivers.SettleRecoveryIdentityProtection,
                IdentityRecoveryReceivers.CompleteEmailRecoveryChallenge,
                IdentityRecoveryReceivers.SendIdentityRecoveryNotice,
            ], "Independent; the notice cannot hold completion hostage."),
        ],
        [IdentityRecoveryReceivers.RejectIdentityRecoveryPreparation],
        CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var content = services.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<IdentityRecoveryIntent>(step.Record.Key.Realm, step.Record.Intent, ct)
            .ConfigureAwait(false);
        return await IdentityRecoveryEffects.CreateAsync(step, intent, services.GetRequiredService<ISourceFactResolver>(),
            content, services.GetRequiredService<IAggregateReader>(), ct).ConfigureAwait(false);
    }
}
