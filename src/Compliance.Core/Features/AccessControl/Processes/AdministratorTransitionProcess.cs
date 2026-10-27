using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>
///     The ordered administrator transition process. Its roots are the three guard facts that admit a transition;
///     the apply stage lists the six source receivers and the complete stage the two guard receivers. The factory
///     reads the frozen intent and selects exactly one apply receiver by its transition kind, then the matching
///     completion receiver; the neutral kinds have no completion. A step the kind did not select is skipped.
/// </summary>
public static class AdministratorTransitionProcess
{
    /// <summary>The catalog name of the process.</summary>
    public const string Name = "AdministratorTransitionProcess";

    /// <summary>The registered definition; it equals the catalog entry exactly.</summary>
    public static ProcessDefinition Definition { get; } = new(Name,
        [nameof(TenantManagerWithdrawalAccepted), nameof(TenantManagerRestorationAccepted),
         nameof(TenantManagerSourceMutationAdmitted)],
        true,
        [
            new ProcessStageDefinition("apply",
            [
                AdministratorReceivers.ApplyMemberWithdrawal,
                AdministratorReceivers.ApplyAdministratorAssignmentWithdrawal,
                AdministratorReceivers.ApplyMemberRestoration,
                AdministratorReceivers.ApplyAdministratorAssignmentRestoration,
                AdministratorReceivers.ApplyNeutralMemberSourceMutation,
                AdministratorReceivers.ApplyNeutralAdministratorAssignmentMutation,
            ], "Exactly one receiver per guard fact, selected by TransitionKind; the guard fact is always first."),
            new ProcessStageDefinition("complete",
            [
                AdministratorReceivers.CompleteAdministratorWithdrawal,
                AdministratorReceivers.CompleteAdministratorRestoration,
            ], "Neutral mutations have no completion fact."),
        ],
        [],
        CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var content = services.GetRequiredService<IImmutableContentStore>();
        var intent = await content.ReadJsonAsync<AdministratorTransitionIntent>(step.Record.Key.Realm,
            step.Record.Intent, ct).ConfigureAwait(false);
        if (step.Receiver != AdministratorTransitionEffects.ApplyReceiver(intent.TransitionKind) &&
            step.Receiver != AdministratorTransitionEffects.CompleteReceiver(intent.TransitionKind))
            return null;
        return await AdministratorTransitionEffects.CreateAsync(step, intent,
            services.GetRequiredService<ISourceFactResolver>(), content, ct).ConfigureAwait(false);
    }
}
