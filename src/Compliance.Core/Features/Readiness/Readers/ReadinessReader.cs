using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Readiness.Readers;

/// <summary>
///     Reads readiness from its own owner streams and composes the public views. An assessment is visible once the
///     program readiness adopted it; its content comes from the assessment owner, each gap's current plan from the plan
///     owner of that gap, and decisions from the program readiness. Nothing is read from a legacy ledger or a
///     projection, and nothing is retained as command state.
/// </summary>
public sealed class ReadinessReader(IAggregateReader reader, IImmutableContentStore content, IDomainEventReader events)
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
        if (tenantId != plan.TenantId)
            throw new ImmutableContentIntegrityException("The gap plan belongs to another tenant.");
        if (plan.Latest is not { } latest)
            return null;
        var view = await ReadinessContent.ReadAsync<ReadinessGapPlanView>(content, tenantId, latest.PlanContent, ct)
            .ConfigureAwait(false);
        if (view.GapId != plan.GapId || view.Revision != latest.Revision ||
            view.PlannedBy != latest.Operation.Actor || view.PlannedAt != latest.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The immutable plan differs from its native owner or original actor.");
        return view;
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
        Uuid assessmentId, CancellationToken ct = default)
    {
        if (tenantId != program.TenantId)
            throw new ImmutableContentIntegrityException("The readiness owner belongs to another tenant.");
        var recorded = (await ReadinessHistory.ReadAsync(events, program, ct).ConfigureAwait(false))
            .OfType<ReadinessDecisionRecorded>().SingleOrDefault(fact => fact.AssessmentId == assessmentId);
        return recorded is null ? null : await ReadDecisionViewAsync(tenantId, program, recorded, ct).ConfigureAwait(false);
    }

    /// <summary>The Type I entry decision recorded on an assessment, or null.</summary>
    public async ValueTask<TypeIEntryDecisionView?> ReadEntryDecisionAsync(Uuid tenantId, ProgramReadiness program,
        Uuid assessmentId, CancellationToken ct = default)
    {
        if (tenantId != program.TenantId)
            throw new ImmutableContentIntegrityException("The readiness owner belongs to another tenant.");
        var recorded = (await ReadinessHistory.ReadAsync(events, program, ct).ConfigureAwait(false))
            .OfType<TypeIEntryDecisionRecorded>().SingleOrDefault(fact => fact.AssessmentId == assessmentId);
        return recorded is null ? null : await ReadEntryViewAsync(tenantId, program, recorded, ct).ConfigureAwait(false);
    }

    /// <summary>Every Type I entry decision of the program, newest first.</summary>
    public async ValueTask<IReadOnlyList<TypeIEntryDecisionView>> ReadEntryDecisionsAsync(Uuid tenantId,
        ProgramReadiness program, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (tenantId != program.TenantId)
            throw new ImmutableContentIntegrityException("The readiness owner belongs to another tenant.");
        var history = await ReadinessHistory.ReadAsync(events, program, ct).ConfigureAwait(false);
        var views = new List<TypeIEntryDecisionView>();
        foreach (var recorded in history.OfType<TypeIEntryDecisionRecorded>().Reverse())
            views.Add(await ReadEntryViewAsync(tenantId, program, recorded, ct).ConfigureAwait(false));
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
        if (tenantId != program.TenantId)
            throw new ImmutableContentIntegrityException("The readiness owner belongs to another tenant.");
        var history = await ReadinessHistory.ReadAsync(events, program, ct).ConfigureAwait(false);
        if (!history.OfType<ReadinessAssessmentAdopted>().Any(fact => fact.Operation.RootOperationId == assessmentId))
            return null;
        var loaded = await ReadContentAsync(tenantId, program.ProgramId, assessmentId, ct).ConfigureAwait(false)
            ?? throw new ImmutableContentUnavailableException("The adopted assessment content is not visible yet.");
        var recorded = loaded.Fact;
        var gaps = await WithPlansAsync(tenantId, program.ProgramId, loaded.Gaps, ct).ConfigureAwait(false);
        return new ReadinessAssessmentView(tenantId, program.ProgramId, assessmentId, program.Revision,
            recorded.RuleVersion, recorded.AsOf, recorded.EditionId, recorded.InputFingerprint, loaded.Inputs,
            loaded.Findings, gaps,
            loaded.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet), gaps.Count,
            recorded.RunBy, recorded.RunAt,
            history.OfType<ReadinessDecisionRecorded>().SingleOrDefault(fact => fact.AssessmentId == assessmentId) is { } decision
                ? await ReadDecisionViewAsync(tenantId, program, decision, ct).ConfigureAwait(false) : null,
            history.OfType<TypeIEntryDecisionRecorded>().SingleOrDefault(fact => fact.AssessmentId == assessmentId) is { } entry
                ? await ReadEntryViewAsync(tenantId, program, entry, ct).ConfigureAwait(false) : null)
        {
            AccessReviewScopes = loaded.AccessReviewScopes,
        };
    }

    /// <summary>The summaries of every adopted assessment, newest first.</summary>
    public async ValueTask<IReadOnlyList<ReadinessAssessmentSummaryView>> ReadSummariesAsync(Uuid tenantId,
        ProgramReadiness program, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (tenantId != program.TenantId)
            throw new ImmutableContentIntegrityException("The readiness owner belongs to another tenant.");
        var history = await ReadinessHistory.ReadAsync(events, program, ct).ConfigureAwait(false);
        var decisions = history.OfType<ReadinessDecisionRecorded>().ToDictionary(static fact => fact.AssessmentId);
        var summaries = new List<ReadinessAssessmentSummaryView>();
        foreach (var adoption in history.OfType<ReadinessAssessmentAdopted>().Reverse())
        {
            var assessmentId = adoption.Operation.RootOperationId;
            var loaded = await ReadContentAsync(tenantId, program.ProgramId, assessmentId, ct).ConfigureAwait(false)
                ?? throw new ImmutableContentUnavailableException("The adopted assessment content is not visible yet.");
            var recorded = loaded.Fact;
            summaries.Add(new ReadinessAssessmentSummaryView(assessmentId, recorded.RuleVersion, recorded.AsOf,
                loaded.Findings.Count(static finding => finding.Outcome == ReadinessRules.RuleMet), loaded.Gaps.Count,
                recorded.RunBy, recorded.RunAt,
                decisions.TryGetValue(assessmentId, out var decision)
                    ? (await ReadDecisionViewAsync(tenantId, program, decision, ct).ConfigureAwait(false)).Outcome : null));
        }

        return summaries;
    }

    /// <summary>The annotations on the gaps of one adopted assessment in recording order, or null when it is not adopted.</summary>
    public async ValueTask<IReadOnlyList<ReadinessAnnotationView>?> ReadAnnotationsAsync(Uuid tenantId,
        ProgramReadiness program, Uuid assessmentId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (tenantId != program.TenantId)
            throw new ImmutableContentIntegrityException("The readiness owner belongs to another tenant.");
        if (!await HasAssessmentAsync(program, assessmentId, ct).ConfigureAwait(false))
            return null;
        var loaded = await ReadContentAsync(tenantId, program.ProgramId, assessmentId, ct).ConfigureAwait(false)
            ?? throw new ImmutableContentUnavailableException("The adopted assessment content is not visible yet.");
        var current = program.LatestAssessmentId == assessmentId;
        var annotations = new List<ReadinessAnnotationView>();
        foreach (var gap in loaded.Gaps)
        {
            var owner = await reader.HydrateAsync(new ReadinessGapPlan(tenantId, program.ProgramId, gap.GapId), ct)
                .ConfigureAwait(false);
            foreach (var fact in (await ReadinessHistory.ReadAsync(events, owner, ct).ConfigureAwait(false))
                         .OfType<ReadinessGapAnnotated>())
            {
                var view = await ReadinessContent.ReadAsync<ReadinessAnnotationView>(content, tenantId,
                    fact.AnnotationContent, ct).ConfigureAwait(false);
                if (view.GapId != owner.GapId || view.AnnotationId != fact.Operation.RootOperationId ||
                    view.AuthoredBy != fact.Operation.Actor || view.AnnotatedAt != fact.Operation.AcceptedAt)
                    throw new ImmutableContentIntegrityException("The annotation differs from its native source.");
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
        if (tenantId != program.TenantId)
            throw new ImmutableContentIntegrityException("The readiness owner belongs to another tenant.");
        foreach (var adoption in (await ReadinessHistory.ReadAsync(events, program, ct).ConfigureAwait(false))
                     .OfType<ReadinessAssessmentAdopted>().Reverse())
            if (await ReadContentAsync(tenantId, program.ProgramId, adoption.Operation.RootOperationId, ct).ConfigureAwait(false) is { } loaded &&
                loaded.Gaps.Any(gap => gap.GapId == gapId))
                return true;
        return false;
    }
    /// <summary>Whether an exact captured program history adopted this assessment.</summary>
    public async ValueTask<bool> HasAssessmentAsync(ProgramReadiness program, Uuid assessmentId, CancellationToken ct = default) =>
        (await ReadinessHistory.ReadAsync(events, program, ct).ConfigureAwait(false))
        .OfType<ReadinessAssessmentAdopted>().Any(fact => fact.Operation.RootOperationId == assessmentId);

    async ValueTask<ReadinessDecisionView> ReadDecisionViewAsync(Uuid tenant, ProgramReadiness program,
        ReadinessDecisionRecorded recorded, CancellationToken ct)
    {
        var view = await ReadinessContent.ReadAsync<ReadinessDecisionView>(content, tenant, recorded.DecisionContent, ct)
            .ConfigureAwait(false);
        if (tenant != program.TenantId || view.AssessmentId != recorded.AssessmentId ||
            view.DecisionId != recorded.DecisionId || view.DecidedBy != recorded.Operation.Actor)
            throw new ImmutableContentIntegrityException("The immutable decision differs from its exact native source.");
        return view;
    }

    async ValueTask<TypeIEntryDecisionView> ReadEntryViewAsync(Uuid tenant, ProgramReadiness program,
        TypeIEntryDecisionRecorded recorded, CancellationToken ct)
    {
        var view = await ReadinessContent.ReadAsync<TypeIEntryDecisionView>(content, tenant, recorded.DecisionContent, ct)
            .ConfigureAwait(false);
        if (tenant != program.TenantId || view.AssessmentId != recorded.AssessmentId ||
            view.DecisionId != recorded.DecisionId || view.DecidedBy != recorded.Operation.Actor)
            throw new ImmutableContentIntegrityException("The immutable entry decision differs from its native source.");
        return view;
    }
}
