using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.Readiness.Processes;

/// <summary>
///     The ordered readiness run process: compute the assessment on its own owner purely from the frozen inputs, then
///     adopt the exact computed assessment under the exact pending adoption generation the admission reserved. The
///     root is the run admission, so the complete input snapshot exists on the program readiness before anything is
///     computed. The adopting receiver builds its proofs from the recorded computation the process kept, so a crash
///     between the two receivers never loses it.
/// </summary>
static class ReadinessRunProcess
{
    public const string Compute = "compute";
    public const string Adopt = "adopt";

    public static ProcessDefinition Definition() => new(ReadinessEffects.RunProcessName,
        [nameof(ReadinessAssessmentRequested)], true,
        [
            new ProcessStageDefinition(Compute, [nameof(ComputeReadinessAssessment)],
                "Pure child computation from the frozen snapshot."),
            new ProcessStageDefinition(Adopt, [nameof(AdoptReadinessAssessment)], "Exact pending adoption generation."),
        ], [], Receiver);

    static async ValueTask<IRequest<ReceiverOutcome>?> Receiver(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var tenant = Uuid.Parse(record.Key.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var content = services.GetRequiredService<IImmutableContentStore>();
        ReadinessRunIntent intent;
        try
        {
            intent = await ReadinessContent.ReadAsync<ReadinessRunIntent>(content, tenant, record.Intent, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            throw new SourceFactVerificationException($"The frozen run cannot be verified yet: {exception.Message}");
        }

        var root = record.Key.RootOperationId;
        var generation = (long)intent.AdoptionGeneration;
        switch (step.Receiver)
        {
            case nameof(ComputeReadinessAssessment):
                return new ComputeReadinessAssessment(step.CommandId, record.Source,
                    new ReadinessAssessment(tenant, intent.ProgramId, intent.AssessmentId).Stream, root, generation,
                    null, record.Intent, intent.ProgramId, intent.AssessmentId);
            case nameof(AdoptReadinessAssessment):
                {
                    var recorded = record.Facts.GetValueOrDefault(nameof(ComputeReadinessAssessment))
                        ?? throw new SourceFactVerificationException(
                            "The recorded assessment is not kept on the process yet.");
                    return new AdoptReadinessAssessment(step.CommandId, recorded,
                        new ProgramReadiness(tenant, intent.ProgramId).Stream, root, generation, null, record.Intent,
                        intent.ProgramId, intent.AssessmentId, generation, record.Source);
                }
            default:
                throw new InvalidOperationException($"{step.Receiver} is not a readiness run receiver.");
        }
    }
}
