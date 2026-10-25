using System.Globalization;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Freezes one active authority and changed component. Narrative and decision chronology is never copied into native facts.</summary>
public static class ComplianceIncidentContentStore
{
    public const string AuthoritySchema = "compliance-incident-current-authority.v1";
    public const string ChangeSchema = "compliance-incident-mutation.v1";
    public const string VersionSchema = "compliance-incident-version.v1";
    public const string AssessmentSchema = "compliance-incident-assessment.v1";
    public const string DecisionSchema = "compliance-incident-description-decision.v1";
    public const string ResolutionSchema = "compliance-incident-resolution.v1";
    public static ComplianceIncidentView CurrentOnly(ComplianceIncidentView state) => state with
    {
        Versions = [state.Current!],
        Assessments = state.CurrentAssessment is { } assessment ? [assessment] : [],
        SystemDescriptionDecisions = state.CurrentSystemDescriptionDecision is { } decision ? [decision] : [],
    };
    public static ComplianceIncidentAuthorityHeader Header(ComplianceIncidentView state, ComplianceIncidentCurrentAuthority authority, string history)
    {
        var current = state.Current ?? throw new ImmutableContentIntegrityException("The incident has no current version.");
        var assessment = state.CurrentAssessment;
        return new(state.TenantId, state.ProgramId, state.ComplianceIncidentId, state.Revision, state.CurrentVersionNumber,
            current.ContentSha256, current.Content.Title, state.Status, current.Content.HandlingClass, current.Content.OccurredAt, current.Content.DetectedAt,
            current.Content.OwnerMemberId, state.ActorMemberId, state.ActorDisplay, state.UpdatedAt, state.PeriodCloseEligible,
            assessment is null ? null : ComplianceIncidentRules.AssessmentSha256(assessment), assessment?.AssessedByMemberId,
            assessment?.RequiresSystemDescriptionReview ?? false, AuthoredIntentDigest.Compute(authority.Assessments), history);
    }
    public static async ValueTask<FrozenComplianceIncidentMutation> FreezeAsync(IImmutableContentStore content,
        ComplianceIncidentView? before, ComplianceIncidentCurrentAuthority? prior, ComplianceIncidentAuthorityHeader? priorHeader,
        ComplianceIncidentRules rules, string kind, CancellationToken ct)
    {
        if (rules.PendingChanges.Count != 1 || rules.Current is not { } composed)
            throw new ImmutableContentIntegrityException("One accepted incident command requires one exact business transition.");
        var after = CurrentOnly(composed);
        object payload = kind switch
        {
            nameof(RecordComplianceIncident) or nameof(ReviseComplianceIncident) => after.Current!,
            nameof(AssessComplianceIncident) => after.CurrentAssessment!,
            nameof(RecordComplianceIncidentSystemDescriptionDecision) => after.CurrentSystemDescriptionDecision!,
            nameof(RecordComplianceIncidentResolution) => after.Resolution!,
            _ => throw new ImmutableContentIntegrityException("The incident command has another component kind."),
        };
        var delta = await WritePayloadAsync(content, after.TenantId, after.ComplianceIncidentId, payload, ct).ConfigureAwait(false);
        var authority = Advance(prior, kind, delta, payload);
        var current = await WriteAsync(content, after.TenantId, after.ComplianceIncidentId, AuthoritySchema, authority, ct).ConfigureAwait(false);
        var mutation = new ComplianceIncidentMutation(kind, prior is null ? null : ContentDigest.OfJson(prior), ContentDigest.OfJson(authority), delta);
        var change = await WriteAsync(content, after.TenantId, after.ComplianceIncidentId, ChangeSchema, mutation, ct).ConfigureAwait(false);
        return new(before is null ? null : CurrentOnly(before), after, prior, authority, current, change, mutation, payload,
            Header(after, authority, History(priorHeader?.HistorySha256, kind, delta, after.Revision)));
    }
    public static ComplianceIncidentCurrentAuthority Advance(ComplianceIncidentCurrentAuthority? before, string kind, ImmutableContentRef payload, object value)
    {
        var assessment = value as ComplianceIncidentImpactAssessmentView;
        return kind switch
        {
            nameof(RecordComplianceIncident) => new(payload, null, null, null, []),
            nameof(ReviseComplianceIncident) => new(payload, null, null, null, before!.Assessments),
            nameof(AssessComplianceIncident) => before! with
            {
                AssessmentContent = payload,
                DecisionContent = null,
                ResolutionContent = null,
                Assessments = [.. before!.Assessments,new(assessment!.ContentVersion,ComplianceIncidentRules.AssessmentSha256(assessment),
                    assessment.AssessedByMemberId,assessment.AssessedAt,assessment.RequiresSystemDescriptionReview)]
            },
            nameof(RecordComplianceIncidentSystemDescriptionDecision) => before! with { DecisionContent = payload },
            nameof(RecordComplianceIncidentResolution) => before! with { ResolutionContent = payload },
            _ => throw new ImmutableContentIntegrityException("The incident authority has another transition kind."),
        };
    }
    public static string History(string? before, string kind, ImmutableContentRef delta, long revision) => ContentDigest.Sha256(Encoding.UTF8.GetBytes(
        "bdgrz.compliance-incident.history.v1\0" + (before ?? "") + "\n" + kind + "\n" + delta.Sha256 + "\n" + revision.ToString(CultureInfo.InvariantCulture)));
    public static async ValueTask<object> ReadPayloadAsync(IImmutableContentStore content, Uuid tenant, ImmutableContentRef value, CancellationToken ct)
    {
        if (value is null)
            throw new ImmutableContentIntegrityException("The incident mutation lacks its exact component reference.");
        return value.Schema switch
        {
            VersionSchema => await content.ReadJsonAsync<ComplianceIncidentVersionView>(tenant.ToString(), value, ct).ConfigureAwait(false),
            AssessmentSchema => await content.ReadJsonAsync<ComplianceIncidentImpactAssessmentView>(tenant.ToString(), value, ct).ConfigureAwait(false),
            DecisionSchema => await content.ReadJsonAsync<ComplianceIncidentSystemDescriptionDecisionView>(tenant.ToString(), value, ct).ConfigureAwait(false),
            ResolutionSchema => await content.ReadJsonAsync<ComplianceIncidentResolutionView>(tenant.ToString(), value, ct).ConfigureAwait(false),
            _ => throw new ImmutableContentIntegrityException("The incident component has another immutable schema."),
        };
    }
    static Task<ImmutableContentRef> WritePayloadAsync(IImmutableContentStore content, Uuid tenant, Uuid incident, object value, CancellationToken ct) => value switch
    {
        ComplianceIncidentVersionView item => WriteAsync(content, tenant, incident, VersionSchema, item, ct),
        ComplianceIncidentImpactAssessmentView item => WriteAsync(content, tenant, incident, AssessmentSchema, item, ct),
        ComplianceIncidentSystemDescriptionDecisionView item => WriteAsync(content, tenant, incident, DecisionSchema, item, ct),
        ComplianceIncidentResolutionView item => WriteAsync(content, tenant, incident, ResolutionSchema, item, ct),
        _ => throw new ImmutableContentIntegrityException("The incident mutation has another immutable payload."),
    };
    static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenant, Uuid incident, string schema, T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion5(incident, schema + ":" + ContentDigest.OfJson(value)), schema, value, ct);
    public static bool Matches<T>(ImmutableContentRef reference, T value, string schema) where T : notnull
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);
        return reference.Schema == schema && reference.MediaType == ImmutableContentStoreExtensions.JsonMediaType &&
            reference.ContentId != Uuid.Empty && reference.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }
    static bool PayloadMatches(ImmutableContentRef reference, object value) => value switch
    {
        ComplianceIncidentVersionView item => Matches(reference, item, VersionSchema),
        ComplianceIncidentImpactAssessmentView item => Matches(reference, item, AssessmentSchema),
        ComplianceIncidentSystemDescriptionDecisionView item => Matches(reference, item, DecisionSchema),
        ComplianceIncidentResolutionView item => Matches(reference, item, ResolutionSchema),
        _ => false,
    };
    public static bool Verify(FrozenComplianceIncidentMutation frozen, ComplianceIncidentAuthorityHeader? beforeHeader)
    {
        try
        {
            var before = frozen.Before;
            var after = frozen.After;
            var rules = ComplianceIncidentRules.FromView(before, after.TenantId, after.ProgramId, after.ComplianceIncidentId, true);
            var failure = Execute(rules, frozen.Mutation.Kind, frozen.Payload, after.ActorMemberId, after.ActorDisplay, after.UpdatedAt);
            return failure is null && rules.PendingChanges.Count == 1 && rules.Current is { } result &&
                ContentDigest.OfJson(CurrentOnly(result)) == ContentDigest.OfJson(after) &&
                (before is null ? frozen.BeforeAuthority is null && beforeHeader is null : frozen.BeforeAuthority is not null && beforeHeader is not null &&
                    MatchesCurrent(beforeHeader, frozen.BeforeAuthority, before)) &&
                ContentDigest.OfJson(Advance(frozen.BeforeAuthority, frozen.Mutation.Kind, frozen.Mutation.Payload, frozen.Payload)) == ContentDigest.OfJson(frozen.Authority) &&
                frozen.Mutation.BeforeSha256 == (frozen.BeforeAuthority is null ? null : ContentDigest.OfJson(frozen.BeforeAuthority)) &&
                frozen.Mutation.AfterSha256 == ContentDigest.OfJson(frozen.Authority) && PayloadMatches(frozen.Mutation.Payload, frozen.Payload) &&
                Matches(frozen.CurrentContent, frozen.Authority, AuthoritySchema) && Matches(frozen.Change, frozen.Mutation, ChangeSchema) &&
                Header(after, frozen.Authority, History(beforeHeader?.HistorySha256, frozen.Mutation.Kind, frozen.Mutation.Payload, after.Revision)) == frozen.Header;
        }
        catch (InvalidOperationException) { return false; }
        catch (ImmutableContentIntegrityException) { return false; }
    }
    public static CommandFailure? Execute(ComplianceIncidentRules rules, string kind, object value, Uuid member, string display, DateTimeOffset at) => kind switch
    {
        nameof(RecordComplianceIncident) when value is ComplianceIncidentVersionView version => rules.Record(version.Content, member, display, at),
        nameof(ReviseComplianceIncident) when value is ComplianceIncidentVersionView version => rules.Revise(rules.Revision, version.Content, version.ChangeReason, member, display, at),
        nameof(AssessComplianceIncident) when value is ComplianceIncidentImpactAssessmentView assessment => rules.Assess(rules.Revision,
            new(assessment.Significance, assessment.Rationale, assessment.ScopeImpactReviewed, assessment.ControlImpactReviewed, assessment.RiskImpactReviewed,
                assessment.VendorImpactReviewed, assessment.ImpactDomains, assessment.AffectedRecords, assessment.FollowUpWorkIds, assessment.EvidenceArtifactIds,
                assessment.RequiresSystemDescriptionReview, assessment.CustomerDataAffected, assessment.AvailabilitySlaAffected), member, display, at),
        nameof(RecordComplianceIncidentSystemDescriptionDecision) when value is ComplianceIncidentSystemDescriptionDecisionView decision =>
            rules.RecordSystemDescriptionDecision(decision.Decision, decision.DecisionId, decision.Rationale, member, display, at),
        nameof(RecordComplianceIncidentResolution) when value is ComplianceIncidentResolutionView resolution => rules.Resolve(rules.Revision,
            new(resolution.Resolution, resolution.ResolvedAt, resolution.EvidenceArtifactIds, resolution.FollowUpCompletions), member, display, at),
        _ => CommandFailure.InvalidContent("The incident mutation kind differs from its exact component."),
    };
    public static bool MatchesCurrent(ComplianceIncidentAuthorityHeader h, ComplianceIncidentCurrentAuthority authority, ComplianceIncidentView view)
    {
        if (view.Current is not { } current || authority.Assessments is null || view.CurrentVersionNumber != current.Version ||
            !Matches(authority.VersionContent, current, VersionSchema) ||
            (authority.AssessmentContent is { } assessment ? view.CurrentAssessment is not { } a || !Matches(assessment, a, AssessmentSchema) : view.CurrentAssessment is not null) ||
            (authority.DecisionContent is { } decision ? view.CurrentSystemDescriptionDecision is not { } d || !Matches(decision, d, DecisionSchema) : view.CurrentSystemDescriptionDecision is not null) ||
            (authority.ResolutionContent is { } resolution ? view.Resolution is not { } r || !Matches(resolution, r, ResolutionSchema) : view.Resolution is not null) ||
            authority.Assessments.Any(item => item.ContentVersion == view.CurrentVersionNumber) != (view.CurrentAssessment is not null) ||
            authority.Assessments.Select(item => item.ContentVersion).Distinct().Count() != authority.Assessments.Count ||
            authority.Assessments.Any(item => item.ContentVersion < 1 || item.ContentVersion > view.CurrentVersionNumber || item.AssessorMemberId == Uuid.Empty ||
                item.AssessedAt == default || item.AssessmentSha256 is not { Length: 64 }) ||
            view.CurrentAssessment is { } active && (authority.Assessments.Count == 0 ? null : authority.Assessments[^1]) != new ComplianceIncidentAssessmentToken(active.ContentVersion,
                ComplianceIncidentRules.AssessmentSha256(active), active.AssessedByMemberId, active.AssessedAt, active.RequiresSystemDescriptionReview))
            return false;
        return Header(view, authority, h.HistorySha256) == h &&
            ComplianceIncidentRules.FromView(view, h.TenantId, h.ProgramId, h.IncidentId, true).ValidateState(view) is null;
    }
    public static DomainEvent Fact(FrozenComplianceIncidentMutation frozen, long sequence, OperationProvenance operation,
        ImmutableContentRef first, ImmutableContentRef result)
    {
        var h = frozen.Header;
        return frozen.Mutation.Kind switch
        {
            nameof(RecordComplianceIncident) => new ComplianceIncidentRecorded(h.TenantId, h.IncidentId, h.Revision, h.ActorMemberId, h, frozen.CurrentContent, frozen.Change, sequence, 1, operation, first, result, operation.AcceptedAt),
            nameof(ReviseComplianceIncident) => new ComplianceIncidentRevised(h.TenantId, h.IncidentId, h.Revision, h.ActorMemberId, h, frozen.CurrentContent, frozen.Change, sequence, 1, operation, first, result, operation.AcceptedAt),
            nameof(AssessComplianceIncident) => new ComplianceIncidentImpactAssessed(h.TenantId, h.IncidentId, h.Revision, h.ActorMemberId, h, frozen.CurrentContent, frozen.Change, sequence, 1, operation, first, result, operation.AcceptedAt),
            nameof(RecordComplianceIncidentSystemDescriptionDecision) => new ComplianceIncidentSystemDescriptionDecisionRecorded(h.TenantId, h.IncidentId, h.Revision, h.ActorMemberId, h, frozen.CurrentContent, frozen.Change, sequence, 1, operation, first, result, operation.AcceptedAt),
            nameof(RecordComplianceIncidentResolution) => new IncidentResolutionRecorded(h.TenantId, h.IncidentId, h.Revision, h.ActorMemberId, h, frozen.CurrentContent, frozen.Change, sequence, 1, operation, first, result, operation.AcceptedAt),
            _ => throw new ImmutableContentIntegrityException("The incident fact has another mutation kind."),
        };
    }
}
