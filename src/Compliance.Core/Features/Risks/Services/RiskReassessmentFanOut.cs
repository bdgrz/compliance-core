using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.Risks.Services;

/// <summary>
///     The reactor-owned fan-out of a reassessment source event: one <see cref="RaiseRiskReassessmentTriggers" /> per
///     assessed risk of the program, each with a command id derived from the source event and the risk so a replayed
///     reaction re-sends identical commands. Discovery requires the risk directory to have reached the authoritative
///     tail of the risk source, so a lagging projection fails transiently and the reaction retries instead of skipping a
///     risk.
/// </summary>
static class RiskReassessmentFanOut
{
    /// <summary>The effect kind that names a reassessment trigger command in its deterministic id.</summary>
    public const string EffectKind = "raise_risk_reassessment_trigger";

    /// <summary>The command a source event causes for one risk.</summary>
    public static RaiseRiskReassessmentTriggers Command(Uuid sourceEventId, Uuid tenantId, Uuid programId, Uuid riskId,
        string triggerKind, string sourceReference, DateTimeOffset raisedAt, Uuid? methodVersionId) =>
        new(EffectIdentity.CommandId(sourceEventId, EffectKind, $"{tenantId}:{programId}:{riskId}", null), tenantId,
            programId, triggerKind, sourceReference, raisedAt, methodVersionId, riskId);

    /// <summary>Sends one trigger command for every assessed risk of the program that the change affects.</summary>
    public static async ValueTask SendAsync(IRequestBus bus, IAggregateReader reader, IRiskDraftDirectoryReader risks,
        RiskDraftListReadConsistency consistency, IReactorContext context, Uuid sourceEventId, Uuid tenantId,
        Uuid programId, string triggerKind, string sourceReference, DateTimeOffset raisedAt, Uuid? methodVersionId,
        CancellationToken ct)
    {
        var caughtUp = await consistency.EnsureCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            throw new ReactionCommandFailedException(caughtUp.Error!);
        string? cursor = null;
        do
        {
            var page = await risks.ListProgramAsync(tenantId, programId, 200, cursor, ct).ConfigureAwait(false);
            foreach (var risk in page.Items.Where(item => item.TenantId == tenantId && item.ProgramId == programId))
            {
                var evaluation = await reader.HydrateAsync(new RiskEvaluation(tenantId, risk.RiskId), ct)
                    .ConfigureAwait(false);
                if (evaluation.Header?.InherentMethodVersionId is not { } inherentMethod)
                    continue;
                if (methodVersionId is { } method && inherentMethod == method)
                    continue;
                await bus.SendReactionAsync(Command(sourceEventId, tenantId, programId, risk.RiskId, triggerKind,
                    sourceReference, raisedAt, methodVersionId), context, ct).ConfigureAwait(false);
            }

            cursor = page.NextCursor;
        } while (cursor is not null);
    }
}
