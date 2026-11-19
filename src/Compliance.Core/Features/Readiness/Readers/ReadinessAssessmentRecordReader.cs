using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Readers;

public sealed class ReadinessAssessmentRecordReader(IAggregateReader reader, IImmutableContentStore content)
{
    public async ValueTask<ReadinessAssessmentResult?> ReadAsync(Uuid tenantId, Uuid programId,
        Uuid assessmentId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ReadinessAssessmentRecord(tenantId, assessmentId), ct).ConfigureAwait(false);
        if (owner.Recorded is not { } fact || fact.ProgramId != programId)
            return null;
        var result = await content.ReadJsonAsync<ReadinessAssessmentResult>(tenantId.ToString(), fact.ResultContent, ct).ConfigureAwait(false);
        var completeness = ReadinessCompletenessRules.Evaluate(result.Inputs, result.Gaps);
        if (result.TenantId != tenantId || result.ProgramId != programId || result.AssessmentId != assessmentId ||
            result.RuleVersion != fact.RuleVersion || result.AsOf != fact.AsOf || result.RequestedBy != fact.RequestedBy ||
            result.RecordedAt != fact.RecordedAt || result.ScheduledDay != fact.ScheduledDay ||
            result.Completeness.IsComplete != completeness.IsComplete || result.Completeness.Verdict != completeness.Verdict ||
            !result.Completeness.IncompleteInputs.SequenceEqual(completeness.IncompleteInputs))
            throw new ImmutableContentIntegrityException("The immutable assessment result differs from its owner or input completeness.");
        return result;
    }
}
