using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Processes;

/// <summary>
///     The unordered client independence preview process: one receiver materializes the immutable preview on the
///     decision owner of the admitted evaluation, from the frozen intent only. There is no abort, because an admitted
///     evaluation can always be computed from its frozen inputs.
/// </summary>
static class ClientIndependencePreviewProcess
{
    public const string Materialize = "materialize";

    public static ProcessDefinition Definition() => new(ClientIndependencePreviewEffects.ProcessName,
        [nameof(ClientIndependenceEvaluationAdmitted)], false,
        [new ProcessStageDefinition(Materialize, [nameof(MaterializeClientIndependencePreview)])], [], Receiver);

    /// <summary>The evaluation is the root operation; its decision owner lives in the realm of the client tenant.</summary>
    static ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var tenantId = Uuid.Parse(record.Key.Realm, CultureInfo.InvariantCulture);
        var evaluationId = record.Key.RootOperationId;
        return ValueTask.FromResult<IRequest<ReceiverOutcome>?>(step.Receiver switch
        {
            nameof(MaterializeClientIndependencePreview) => new MaterializeClientIndependencePreview(step.CommandId,
                record.Source, new ClientIndependenceDecision(tenantId, evaluationId).Stream, evaluationId,
                ClientIndependencePreviewEffects.Generation, null, record.Intent, evaluationId),
            _ => throw new InvalidOperationException($"{step.Receiver} is not a preview receiver."),
        });
    }
}
