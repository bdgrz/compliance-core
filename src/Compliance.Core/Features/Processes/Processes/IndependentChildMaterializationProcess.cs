using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>
///     The unordered process that materializes one independent child from its admission. Its roots are the
///     admission facts of the parents that admit a child before creating it; each admitted child has one receiver and
///     there is no ordering between receivers. The definition equals the catalog entry; the command of each receiver
///     is built by the <see cref="IChildMaterializationReceiver" /> its owner registers, and a root whose owner is
///     not registered is never started because no reactor produces its record.
/// </summary>
public static class IndependentChildMaterializationProcess
{
    /// <summary>The catalog name of the process.</summary>
    public const string Name = "IndependentChildMaterialization";

    /// <summary>The registered definition; it equals the catalog entry exactly.</summary>
    public static ProcessDefinition Definition() => new(Name,
        [
            "AdvisorFeedbackAdmitted",
            "AuditorResponseAdmitted",
            "AuditorCommentAdmitted",
            "CorrectiveActionAdmitted",
            "AuditorSampleSelectionAdmitted",
            "ProviderCoverageGapRiskAcceptanceAdmitted",
            "PolicyParticipantResultAdmitted",
        ], false,
        [
            new ProcessStageDefinition("materialize",
            [
                "MaterializeAdvisorFeedback",
                "MaterializeAuditorResponse",
                "MaterializeAuditorComment",
                "MaterializeCorrectiveAction",
                "MaterializeAuditorSampleSelection",
                "MaterializeProviderRiskAcceptanceLink",
                "ApplyPolicyParticipantResult",
            ], "Single receiver per admitted child; no cross-receiver ordering. Parent closure drains these before its own acquisition stage."),
        ], [], CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        foreach (var receiver in services.GetServices<IChildMaterializationReceiver>())
        {
            if (string.Equals(receiver.Receiver, step.Receiver, StringComparison.Ordinal))
                return await receiver.CreateAsync(step, services, ct).ConfigureAwait(false);
        }

        return null;
    }
}
