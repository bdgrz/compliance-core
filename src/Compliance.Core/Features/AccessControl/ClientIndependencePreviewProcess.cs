using System.Globalization;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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
