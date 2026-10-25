using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>Compact native incident authority. Immutable history, sources and required completion proofs never become retained Aggregate state.</summary>
public sealed class ComplianceIncident : Aggregate
{
    public const string Area = "compliance-incidents";
    readonly Uuid _tenantId;
    readonly Uuid _requestedProgramId;
    public ComplianceIncident(Uuid tenantId, Uuid programId, Uuid incidentId, Uuid selectedCommandId = default)
        : base(incidentId, new EventStreamAddress(tenantId.ToString(), Area, incidentId.ToString()))
    {
        _tenantId = tenantId;
        _requestedProgramId = programId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<ComplianceIncidentRecorded>(ApplyFact);
        On<ComplianceIncidentRevised>(ApplyFact);
        On<ComplianceIncidentImpactAssessed>(ApplyFact);
        On<ComplianceIncidentSystemDescriptionDecisionRecorded>(ApplyFact);
        On<IncidentResolutionRecorded>(ApplyFact);
    }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => Header?.ProgramId ?? _requestedProgramId;
    public long Revision => Header?.Revision ?? 0;
    public long NativeSequence { get; private set; }
    public ComplianceIncidentAuthorityHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public NativeAcceptance Acceptance { get; }
    public static string AssessmentSha256(ComplianceIncidentImpactAssessmentView assessment) => ComplianceIncidentRules.AssessmentSha256(assessment);
    public Result Append(DomainEvent value, FrozenComplianceIncidentMutation prepared, long sequence, ulong position)
    {
        if (_requestedProgramId == Uuid.Empty || prepared.Header.ProgramId != _requestedProgramId)
            return Fail("The incident mutation belongs to another requested program scope.");
        if (NativeSequence != sequence || CommittedStreamPosition != position)
            return Fail("The incident changed while its exact source and current authority were prepared.", true);
        if (value is not IComplianceIncidentFact fact || fact.Header != prepared.Header || fact.CurrentContent != prepared.CurrentContent ||
            fact.Change != prepared.Change || (Header is null ? prepared.Before is not null || prepared.BeforeAuthority is not null :
                prepared.Before is null || prepared.BeforeAuthority is null || CurrentContent is null ||
                !ComplianceIncidentContentStore.Matches(CurrentContent, prepared.BeforeAuthority, ComplianceIncidentContentStore.AuthoritySchema)) ||
            !ComplianceIncidentContentStore.Verify(prepared, Header))
            return Fail("The incident mutation does not carry its exact verified local business transition.");
        try
        {
            Validate(value, fact);
            if (DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
                return Fail("The compact native incident fact exceeds the bounded event envelope.");
        }
        catch (InvalidOperationException) { return Fail("The incident mutation has an invalid native authority binding."); }
        RaiseEvent(value);
        return Result.Success;
    }
    void ApplyFact(DomainEvent value)
    {
        var fact = (IComplianceIncidentFact)value;
        Validate(value, fact);
        Header = fact.Header;
        CurrentContent = fact.CurrentContent;
        NativeSequence = fact.NativeSequence;
        Acceptance.Observe(value);
    }
    void Validate(DomainEvent value, IComplianceIncidentFact fact)
    {
        var h = fact.Header;
        var op = fact.Operation;
        if (h is null || op is null || op.Actor is null)
            throw new InvalidOperationException("A native incident requires complete current authority and its exact operation.");
        OwnerProvenance.RequireOwn(this, op.LogicalId, op);
        var kind = value switch
        {
            ComplianceIncidentRecorded => nameof(RecordComplianceIncident),
            ComplianceIncidentRevised => nameof(ReviseComplianceIncident),
            ComplianceIncidentImpactAssessed => nameof(AssessComplianceIncident),
            ComplianceIncidentSystemDescriptionDecisionRecorded => nameof(RecordComplianceIncidentSystemDescriptionDecision),
            IncidentResolutionRecorded => nameof(RecordComplianceIncidentResolution),
            _ => "",
        };
        var reaction = kind == nameof(RecordComplianceIncidentSystemDescriptionDecision);
        if (fact.TenantId != _tenantId || h.TenantId != _tenantId || h.ProgramId == Uuid.Empty || h.IncidentId != Id || fact.SubjectId != Id ||
            fact.Revision != h.Revision || h.Revision <= 0 || h.CurrentVersionNumber < 1 || fact.NativeSequence != NativeSequence + 1 ||
            fact.ChangeCount != 1 || fact.ActorMemberId != h.ActorMemberId || h.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(h.ActorDisplay) || h.ActorDisplay.Length > 200 || string.IsNullOrWhiteSpace(h.Title) || h.Title.Length > 300 ||
            h.Status is not ("recorded" or "assessed" or "needs_review" or "resolved") ||
            h.HandlingClass is not ("public" or "internal" or "confidential" or "restricted") || h.OwnerMemberId == Uuid.Empty ||
            h.OccurredAt == default || h.DetectedAt < h.OccurredAt || h.UpdatedAt == default ||
            !Hex(h.ContentSha256) || !Hex(h.AssessmentHistorySha256) || !Hex(h.HistorySha256) ||
            h.CurrentAssessmentSha256 is { } assessment && !Hex(assessment) ||
            fact.OccurredAt != op.AcceptedAt || op.AcceptedAt == default || op.CommandKind != kind || op.RootOperationId != op.LogicalId ||
            op.CausationEventId is not null || op.Episode != Id.ToString() || op.Owner != Stream || op.FirstOwner != Stream ||
            op.AffectedOwner != Stream || op.ExpectedRevisionOwner != Stream || !Hex(op.AuthoredIntentSha256) ||
            !reaction && (op.Actor.Kind != "member" || !Uuid.TryParse(op.Actor.Id, out var member) || member != fact.ActorMemberId) ||
            reaction && op.Actor.Kind != "system_process" ||
            !Reference(fact.CurrentContent, ComplianceIncidentContentStore.AuthoritySchema) || !Reference(fact.Change, ComplianceIncidentContentStore.ChangeSchema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1"))
            throw new InvalidOperationException("The incident fact has another exact native scope, actor, content or original result binding.");
        if (Header is not { } prior)
        {
            if (kind != nameof(RecordComplianceIncident) || h.Revision != 1 || h.CurrentVersionNumber != 1 ||
                h.Status != "recorded" || op.LogicalId != Id || op.OriginalExpectedRevision is not null)
                throw new InvalidOperationException("An incident begins with its own identified initial report.");
            return;
        }
        if (kind == nameof(RecordComplianceIncident) || h.ProgramId != prior.ProgramId || h.Revision != prior.Revision + 1 ||
            h.OccurredAt != prior.OccurredAt || h.DetectedAt != prior.DetectedAt ||
            h.CurrentVersionNumber != prior.CurrentVersionNumber + (kind == nameof(ReviseComplianceIncident) ? 1 : 0) ||
            reaction && op.OriginalExpectedRevision is not null || !reaction && op.OriginalExpectedRevision != unchecked((ulong)prior.Revision))
            throw new InvalidOperationException("An incident transition cannot rewrite identity, source chronology or its original revision gate.");
    }
    static bool Reference(ImmutableContentRef? value, string schema) => value is { Bytes: > 0 } && value.ContentId != Uuid.Empty &&
        value.Schema == schema && value.MediaType == ImmutableContentStoreExtensions.JsonMediaType && Hex(value.Sha256);
    static bool Hex(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static Result Fail(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
