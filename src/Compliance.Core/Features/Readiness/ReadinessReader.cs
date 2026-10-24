using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     Reads readiness from its own owner streams and composes the public views. An assessment is visible once the
///     program readiness adopted it; its content comes from the assessment owner, each gap's current plan from the plan
///     owner of that gap, and decisions from the program readiness. Nothing is read from a legacy ledger or a
///     projection, and nothing is retained as command state.
/// </summary>
public sealed class ReadinessReader(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>The verified content of one recorded assessment, without any current plan.</summary>
    public sealed record AssessmentContent(ReadinessAssessmentRecorded Fact, IReadOnlyList<ReadinessInputView> Inputs,
        IReadOnlyList<ReadinessFindingView> Findings, IReadOnlyList<ReadinessGapView> Gaps,
        IReadOnlyList<ReadinessAccessReviewScopeView> AccessReviewScopes);

    /// <summary>Hydrates the program readiness.</summary>
    public ValueTask<ProgramReadiness> ReadProgramAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default) =>
        reader.HydrateAsync(new ProgramReadiness(tenantId, programId), ct);

    /// <summary>The verified content of a recorded assessment, or null when the assessment owner holds none.</summary>
    public async ValueTask<AssessmentContent?> ReadContentAsync(Uuid tenantId, Uuid programId, Uuid assessmentId,
        CancellationToken ct = default)
    {
        var assessment = await reader.HydrateAsync(new ReadinessAssessment(tenantId, programId, assessmentId), ct)
            .ConfigureAwait(false);
        if (assessment.Recorded is not { } recorded)
            return null;
        return new AssessmentContent(recorded,
            await ReadinessContent.ReadAsync<IReadOnlyList<ReadinessInputView>>(content, tenantId,
                recorded.InputsManifest, ct).ConfigureAwait(false),
            await ReadinessContent.ReadAsync<IReadOnlyList<ReadinessFindingView>>(content, tenantId,
                recorded.FindingsManifest, ct).ConfigureAwait(false),
            await ReadinessContent.ReadAsync<IReadOnlyList<ReadinessGapView>>(content, tenantId, recorded.GapsManifest,
                ct).ConfigureAwait(false),
            await ReadinessContent.ReadAsync<IReadOnlyList<ReadinessAccessReviewScopeView>>(content, tenantId,
                recorded.AccessReviewScopesManifest, ct).ConfigureAwait(false));
    }

    /// <summary>The current plan of a hydrated gap plan owner, or null while the gap is unplanned.</summary>
    public async ValueTask<ReadinessGapPlanView?> ReadPlanAsync(Uuid tenantId, ReadinessGapPlan plan,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Latest is not { } latest)
            return null;
        return (await ReadinessContent.ReadAsync<ReadinessGapPlanView>(content, tenantId, latest.PlanContent, ct)
            .ConfigureAwait(false)) with
        { Revision = latest.Revision };
    }

    /// <summary>The gaps with the current plan of each gap read from its own plan owner.</summary>
    public async ValueTask<IReadOnlyList<ReadinessGapView>> WithPlansAsync(Uuid tenantId, Uuid programId,
        IReadOnlyList<ReadinessGapView> gaps, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gaps);
        var planned = new List<ReadinessGapView>(gaps.Count);
        foreach (var gap in gaps)
        {
            var owner = await reader.HydrateAsync(new ReadinessGapPlan(tenantId, programId, gap.GapId), ct)
                .ConfigureAwait(false);
            planned.Add(gap with { Plan = await ReadPlanAsync(tenantId, owner, ct).ConfigureAwait(false) });
        }

        return planned;
    }

    /// <summary>The recorded readiness decision of an assessment, or null.</summary>
    public async ValueTask<ReadinessDecisionView?> ReadDecisionAsync(Uuid tenantId, ProgramReadiness program,
        Uuid assessmentId, CancellationToken ct = default) =>
        program.DecisionOf(assessmentId) is { } recorded
            ? await ReadinessContent.ReadAsync<ReadinessDecisionView>(content, tenantId, recorded.DecisionContent, ct)
                .ConfigureAwait(false)
            : null;

    /// <summary>The Type I entry decision recorded on an assessment, or null.</summary>
    public async ValueTask<TypeIEntryDecisionView?> ReadEntryDecisionAsync(Uuid tenantId, ProgramReadiness program,
        Uuid assessmentId, CancellationToken ct = default) =>
        program.EntryDecisions.FirstOrDefault(decision => decision.AssessmentId == assessmentId) is { } recorded
            ? await ReadinessContent.ReadAsync<TypeIEntryDecisionView>(content, tenantId, recorded.DecisionContent, ct)
                .ConfigureAwait(false)
            : null;

    /// <summary>Every Type I entry decision of the program, newest first.</summary>
    public async ValueTask<IReadOnlyList<TypeIEntryDecisionView>> ReadEntryDecisionsAsync(Uuid tenantId,
        ProgramReadiness program, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        var views = new List<TypeIEntryDecisionView>(program.EntryDecisions.Count);
        foreach (var recorded in program.EntryDecisions.Reverse())
            views.Add(await ReadinessContent.ReadAsync<TypeIEntryDecisionView>(content, tenantId,
                recorded.DecisionContent, ct).ConfigureAwait(false));
        return views;
    }

    /// <summary>The view of an adopted assessment, or null when the program did not adopt it.</summary>
    public async ValueTask<ReadinessAssessmentView?> ReadViewAsync(Uuid tenantId, Uuid programId, Uuid assessmentId,
        CancellationToken ct = default)
    {
        var program = await ReadProgramAsync(tenantId, programId, ct).ConfigureAwait(false);
        return await ReadViewAsync(tenantId, program, assessmentId, ct).ConfigureAwait(false);
    }

    /// <summary>The view of an adopted assessment of a hydrated program readiness, or null.</summary>
    public async ValueTask<ReadinessAssessmentView?> ReadViewAsync(Uuid tenantId, ProgramReadiness program,
        Uuid assessmentId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.AdoptionOf(assessmentId) is null ||
            await ReadContentAsync(tenantId, program.ProgramId, assessmentId, ct).ConfigureAwait(false) is not { } loaded)
            return null;
        var recorded = loaded.Fact;
        var gaps = await WithPlansAsync(tenantId, program.ProgramId, loaded.Gaps, ct).ConfigureAwait(false);
        return new ReadinessAssessmentView(tenantId, program.ProgramId, assessmentId, program.Revision,
            recorded.RuleVersion, recorded.AsOf, recorded.EditionId, recorded.InputFingerprint, loaded.Inputs,
            loaded.Findings, gaps,
            loaded.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet), gaps.Count,
            recorded.RunBy, recorded.RunAt,
            await ReadDecisionAsync(tenantId, program, assessmentId, ct).ConfigureAwait(false),
            await ReadEntryDecisionAsync(tenantId, program, assessmentId, ct).ConfigureAwait(false))
        {
            AccessReviewScopes = loaded.AccessReviewScopes,
        };
    }

    /// <summary>The summaries of every adopted assessment, newest first.</summary>
    public async ValueTask<IReadOnlyList<ReadinessAssessmentSummaryView>> ReadSummariesAsync(Uuid tenantId,
        ProgramReadiness program, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        var summaries = new List<ReadinessAssessmentSummaryView>(program.AdoptedAssessments.Count);
        foreach (var assessmentId in program.AdoptedAssessments.Reverse())
        {
            var loaded = await ReadContentAsync(tenantId, program.ProgramId, assessmentId, ct).ConfigureAwait(false)
                ?? throw new ImmutableContentIntegrityException("An adopted assessment has no recorded content.");
            var recorded = loaded.Fact;
            summaries.Add(new ReadinessAssessmentSummaryView(assessmentId, recorded.RuleVersion, recorded.AsOf,
                loaded.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet), loaded.Gaps.Count,
                recorded.RunBy, recorded.RunAt,
                (await ReadDecisionAsync(tenantId, program, assessmentId, ct).ConfigureAwait(false))?.Outcome));
        }

        return summaries;
    }

    /// <summary>The annotations on the gaps of one adopted assessment in recording order, or null when it is not adopted.</summary>
    public async ValueTask<IReadOnlyList<ReadinessAnnotationView>?> ReadAnnotationsAsync(Uuid tenantId,
        ProgramReadiness program, Uuid assessmentId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.AdoptionOf(assessmentId) is null ||
            await ReadContentAsync(tenantId, program.ProgramId, assessmentId, ct).ConfigureAwait(false) is not { } loaded)
            return null;
        var current = program.LatestAssessmentId == assessmentId;
        var annotations = new List<ReadinessAnnotationView>();
        foreach (var gap in loaded.Gaps)
        {
            var owner = await reader.HydrateAsync(new ReadinessGapPlan(tenantId, program.ProgramId, gap.GapId), ct)
                .ConfigureAwait(false);
            foreach (var fact in owner.Annotations)
            {
                var view = await ReadinessContent.ReadAsync<ReadinessAnnotationView>(content, tenantId,
                    fact.AnnotationContent, ct).ConfigureAwait(false);
                if (view.AssessmentId == assessmentId)
                    annotations.Add(view with { Current = current });
            }
        }

        return annotations.OrderBy(static annotation => annotation.AnnotatedAt)
            .ThenBy(static annotation => annotation.AnnotationId.ToString(), StringComparer.Ordinal).ToArray();
    }

    /// <summary>The latest adopted assessment of a program, or null before the first adoption.</summary>
    public async ValueTask<Uuid?> GetLatestAssessmentIdAsync(Uuid tenantId, Uuid programId, CancellationToken ct = default) =>
        (await ReadProgramAsync(tenantId, programId, ct).ConfigureAwait(false)).LatestAssessmentId;

    /// <summary>Whether a gap belongs to any assessment the program adopted.</summary>
    public async ValueTask<bool> HasGapAsync(Uuid tenantId, ProgramReadiness program, Uuid gapId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        foreach (var assessmentId in program.AdoptedAssessments.Reverse())
            if (await ReadContentAsync(tenantId, program.ProgramId, assessmentId, ct).ConfigureAwait(false) is { } loaded &&
                loaded.Gaps.Any(gap => gap.GapId == gapId))
                return true;
        return false;
    }
}
