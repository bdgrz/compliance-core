using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.UserIdentities.Processes;

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
            content, services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IDomainEventReader>(), ct).ConfigureAwait(false);
    }
}
