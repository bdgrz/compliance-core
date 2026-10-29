using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Applications.Readers;

/// <summary>
/// Reads known source references from each program's latest readiness assessment. The current
/// assessment contract does not retain every source-to-application relationship, so any existing
/// assessment keeps the full readiness impact context pending.
/// </summary>
sealed class ApplicationReadinessImpactReader(IProgramDirectoryReader programs,
    ReadinessReader readiness, ProgramDirectoryReadConsistency programConsistency) : IApplicationReadinessImpactReader
{
    const int MaximumPrograms = 200;
    const int MaximumReferences = 200;

    public async ValueTask<Result<ApplicationReadinessImpact>> ReadAsync(Uuid tenantId,
        Uuid applicationId, IReadOnlyCollection<Uuid> systemInstanceIds,
        bool systemInstanceListComplete, CancellationToken ct)
    {
        var programFence = await programConsistency.CaptureListFenceAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!programFence.IsSuccess)
            return Result<ApplicationReadinessImpact>.Failure(programFence.Error);

        var programPage = await programs.ListAsync(tenantId, MaximumPrograms, null, ct)
            .ConfigureAwait(false);
        if (programPage.Items.Count > MaximumPrograms ||
            programPage.Items.Any(program => program.TenantId != tenantId ||
                program.ProgramId == Uuid.Empty || string.IsNullOrWhiteSpace(program.Name)) ||
            programPage.Items.Select(static program => program.ProgramId).Distinct().Count() !=
            programPage.Items.Count)
            return Inconsistent("The program directory projection is inconsistent.");

        var references = new List<ApplicationReadinessReferenceView>();
        var complete = systemInstanceListComplete && programPage.NextCursor is null;
        var programsOverLimit = programPage.NextCursor is not null;
        var referencesOverLimit = false;
        var instanceIds = systemInstanceIds.ToHashSet();
        var stop = false;
        foreach (var program in programPage.Items)
        {
            ReadinessAssessmentView? assessment;
            Uuid assessmentId;
            try
            {
                if (await readiness.GetLatestAssessmentIdAsync(tenantId, program.ProgramId, ct)
                        .ConfigureAwait(false) is not { } latestAssessmentId)
                    continue;
                assessmentId = latestAssessmentId;
                assessment = await readiness.ReadViewAsync(tenantId, program.ProgramId, assessmentId, ct)
                    .ConfigureAwait(false);
            }
            catch (Bdgrz.Compliance.Features.Content.Services.ImmutableContentIntegrityException exception)
            {
                return BehindSource($"The readiness content cannot be verified yet: {exception.Message}");
            }

            if (assessment is null || assessment.TenantId != tenantId ||
                assessment.ProgramId != program.ProgramId ||
                assessment.AssessmentId != assessmentId || assessment.Revision < 1 ||
                assessment.AsOf == default || string.IsNullOrWhiteSpace(assessment.RuleVersion) ||
                !IsValidAssessment(assessment, instanceIds, applicationId))
                return BehindSource("The readiness assessment detail projection is incomplete.");

            // Current readiness views do not preserve every path from assessed sources back to
            // an application. A match is useful evidence, but no match cannot prove no impact.
            complete = false;
            if (assessment.Inputs.Any(static input => input.Status is not
                    ("assessed" or "not_assessed")))
                return Inconsistent("The readiness assessment projection is inconsistent.");

            var matched = MatchedSources(assessment, applicationId, instanceIds);
            foreach (var source in matched)
            {
                if (references.Count == MaximumReferences)
                {
                    referencesOverLimit = true;
                    complete = false;
                    stop = true;
                    break;
                }

                references.Add(new ApplicationReadinessReferenceView(program.ProgramId,
                    program.Name, assessment.AssessmentId, assessment.Revision,
                    assessment.RuleVersion, assessment.AsOf, source));
            }
            if (stop)
                break;
        }

        var programConfirmed = await programConsistency.EnsureListFenceHeldAsync(tenantId,
            programFence.Value, ct).ConfigureAwait(false);
        if (!programConfirmed.IsSuccess)
            return Result<ApplicationReadinessImpact>.Failure(programConfirmed.Error);
        return Result<ApplicationReadinessImpact>.Success(new ApplicationReadinessImpact(
            references.OrderBy(static item => item.ProgramId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.AssessmentId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.Source.Kind, StringComparer.Ordinal)
                .ThenBy(static item => item.Source.Id.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.Source.Version, StringComparer.Ordinal).ToArray(),
            complete, programsOverLimit, referencesOverLimit));
    }

    static ReadinessSourceReference[] MatchedSources(
        ReadinessAssessmentView assessment, Uuid applicationId, HashSet<Uuid> instanceIds)
    {
        var candidates = assessment.Findings.SelectMany(static finding => finding.Sources)
            .Concat(assessment.Gaps.SelectMany(static gap => gap.Sources))
            .Concat(assessment.AccessReviewScopes.SelectMany(static scope =>
            {
                var sources = new List<ReadinessSourceReference>
                {
                    new("system_instance", scope.SystemInstanceId, null),
                };
                if (scope.EffectiveDecision is { } decision)
                    sources.Add(new ReadinessSourceReference("access_review_scope_decision",
                        decision.DecisionId,
                        decision.Sequence.ToString(CultureInfo.InvariantCulture)));
                return sources;
            }));
        return candidates.Where(source =>
                source.Kind == "application" && source.Id == applicationId ||
                source.Kind == "system_instance" && instanceIds.Contains(source.Id) ||
                source.Kind == "access_review_scope_decision" &&
                assessment.AccessReviewScopes.Any(scope =>
                    instanceIds.Contains(scope.SystemInstanceId) &&
                    scope.EffectiveDecision?.DecisionId == source.Id))
            .DistinctBy(static source => (source.Kind, source.Id, source.Version))
            .OrderBy(static source => source.Kind, StringComparer.Ordinal)
            .ThenBy(static source => source.Id.ToString(), StringComparer.Ordinal)
            .ThenBy(static source => source.Version, StringComparer.Ordinal)
            .ToArray();
    }

    static bool IsValidAssessment(ReadinessAssessmentView assessment,
        HashSet<Uuid> instanceIds, Uuid applicationId)
    {
        if (assessment.Inputs is null || assessment.Findings is null || assessment.Gaps is null ||
            assessment.AccessReviewScopes is null ||
            assessment.Inputs.Any(static input => input is null ||
                string.IsNullOrWhiteSpace(input.Family) || string.IsNullOrWhiteSpace(input.Status)) ||
            assessment.Findings.Any(static finding => finding is null ||
                finding.Sources is null || finding.Sources.Any(static source =>
                    source is null || string.IsNullOrWhiteSpace(source.Kind) ||
                    source.Id == Uuid.Empty)) ||
            assessment.Gaps.Any(static gap => gap is null || gap.Sources is null ||
                gap.Sources.Any(static source => source is null ||
                    string.IsNullOrWhiteSpace(source.Kind) || source.Id == Uuid.Empty)))
            return false;

        if (assessment.AccessReviewScopes.Any(static scope => scope is null ||
                scope.SystemInstanceId == Uuid.Empty ||
                scope.Status is not ("included" or "excluded" or "unresolved")) ||
            assessment.AccessReviewScopes.Select(static scope => scope.SystemInstanceId)
                .Distinct().Count() != assessment.AccessReviewScopes.Count)
            return false;

        return assessment.AccessReviewScopes.All(scope =>
        {
            if (scope.EffectiveDecision is not { } decision)
                return scope.Status == "unresolved";
            return instanceIds.Contains(scope.SystemInstanceId)
                ? decision.TenantId == assessment.TenantId &&
                  decision.ApplicationId == applicationId &&
                  decision.SystemInstanceId == scope.SystemInstanceId &&
                  decision.DecisionId != Uuid.Empty && decision.Sequence > 0 &&
                  decision.Decision == scope.Status &&
                  scope.Status is "included" or "excluded"
                : decision.TenantId == assessment.TenantId &&
                  decision.SystemInstanceId == scope.SystemInstanceId &&
                  decision.DecisionId != Uuid.Empty && decision.Sequence > 0 &&
                  decision.Decision == scope.Status &&
                  scope.Status is "included" or "excluded";
        });
    }

    static Result<ApplicationReadinessImpact> Inconsistent(string message) =>
        Result<ApplicationReadinessImpact>.Failure(new RequestError(RequestErrorKind.Conflict,
            message, isTransient: true));

    static Result<ApplicationReadinessImpact> BehindSource(string message) =>
        Result<ApplicationReadinessImpact>.Failure(new RequestError(RequestErrorKind.Conflict,
            message, isTransient: true));
}
