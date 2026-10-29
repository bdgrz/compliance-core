using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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
