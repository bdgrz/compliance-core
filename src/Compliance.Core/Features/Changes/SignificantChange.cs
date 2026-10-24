using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Compact native change authority. Governed references, assessments and history remain immutable content.</summary>
public sealed class SignificantChange : Aggregate
{
    public const string Area = "significant-changes";
    readonly Uuid _tenantId;
    readonly Uuid _requestedProgramId;
    public SignificantChange(Uuid tenantId, Uuid programId, Uuid changeId, Uuid selectedCommandId = default)
        : base(changeId, new EventStreamAddress(tenantId.ToString(), Area, changeId.ToString()))
    {
        _tenantId = tenantId;
        _requestedProgramId = programId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<SignificantChangeRecorded>(ApplyFact);
        On<SignificantChangeRevised>(ApplyFact);
        On<SignificantChangeImpactAssessed>(ApplyFact);
        On<ImpactReviewWorkLinked>(ApplyFact);
        On<SignificantChangeSystemDescriptionDecisionRecorded>(ApplyFact);
    }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => Header?.ProgramId ?? _requestedProgramId;
    public long Revision => Header?.Revision ?? 0;
    public SignificantChangeAuthorityHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public long NativeSequence { get; private set; }
    public NativeAcceptance Acceptance { get; }
    public static string AssessmentSha256(SignificantChangeImpactView assessment) => SignificantChangeRules.AssessmentSha256(assessment);

    public Result Append(DomainEvent value, FrozenSignificantChangeMutation prepared, long sequence, ulong position)
    {
        if (NativeSequence != sequence || CommittedStreamPosition != position)
            return Fail("The significant change changed while its exact content was prepared.", true);
        if (value is not ISignificantChangeFact fact ||
            (Header is null ? prepared.Before is not null : prepared.Before is null || SignificantChangeContentStore.Header(prepared.Before) != Header) ||
            fact.Header != prepared.Header || fact.CurrentContent != prepared.Body || fact.Change != prepared.Change ||
            fact.ChangeCount != (prepared.Mutation.Changed ? 1 : 0) ||
            !SignificantChangeContentStore.ValidMutation(prepared.Before, prepared.After, prepared.Mutation) ||
            prepared.Header != SignificantChangeContentStore.Header(prepared.After))
            return Fail("The significant-change mutation does not carry its exact verified local transition.");
        try
        {
            Validate(value, fact);
            if (DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
                return Fail("The native significant-change fact exceeds the bounded event payload size.");
        }
        catch (InvalidOperationException)
        {
            return Fail("The significant-change mutation has an invalid native binding.");
        }
        RaiseEvent(value);
        return Result.Success;
    }
    void ApplyFact(DomainEvent value)
    {
        var fact = (ISignificantChangeFact)value;
        Validate(value, fact);
        Header = fact.Header;
        CurrentContent = fact.CurrentContent;
        NativeSequence = fact.NativeSequence;
        Acceptance.Observe(value);
    }
    void Validate(DomainEvent value, ISignificantChangeFact fact)
    {
        var h = fact.Header;
        var op = fact.Operation;
        var kind = value switch
        {
            SignificantChangeRecorded => nameof(RecordSignificantChange),
            SignificantChangeRevised => nameof(ReviseSignificantChange),
            SignificantChangeImpactAssessed => nameof(AssessSignificantChange),
            ImpactReviewWorkLinked => nameof(LinkImpactReviewWork),
            SignificantChangeSystemDescriptionDecisionRecorded => nameof(RecordSignificantChangeSystemDescriptionDecision),
            _ => "",
        };
        if (op is null || op.Actor is null || h is null)
            throw new InvalidOperationException("A native change requires its exact operation and current authority.");
        OwnerProvenance.RequireOwn(this, op.LogicalId, op);
        var memberCommand = kind is nameof(RecordSignificantChange) or nameof(ReviseSignificantChange) or nameof(AssessSignificantChange);
        if (fact.TenantId != _tenantId || h.TenantId != _tenantId || h.ProgramId == Uuid.Empty || h.SignificantChangeId != Id ||
            fact.SubjectId != Id || fact.Revision != h.Revision || h.Revision <= 0 || h.CurrentVersionNumber < 1 ||
            fact.NativeSequence != NativeSequence + 1 || fact.ActorMemberId is null || fact.ActorMemberId == Uuid.Empty ||
            fact.ChangeCount > 0 && fact.ActorMemberId != h.ActorMemberId || h.ActorMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(h.ActorDisplay) || h.ActorDisplay.Length > 200 || string.IsNullOrWhiteSpace(h.Title) || h.Title.Length > 300 ||
            !Hex(h.ContentSha256) || h.UpdatedAt == default || fact.OccurredAt != op.AcceptedAt || op.AcceptedAt == default ||
            !Reference(fact.CurrentContent, SignificantChangeContentStore.BodySchema) || !Reference(fact.Change, SignificantChangeContentStore.ChangeSchema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1") ||
            op.CommandKind != kind || op.RootOperationId != op.LogicalId || op.CausationEventId is not null || op.Episode != Id.ToString() ||
            op.Owner != Stream || op.AffectedOwner != Stream || op.ExpectedRevisionOwner != Stream || op.FirstOwner != Stream ||
            !Hex(op.AuthoredIntentSha256) || fact.ChangeCount is < 0 or > 1 ||
            memberCommand && (op.Actor.Kind != "member" || !Uuid.TryParse(op.Actor.Id, out var actor) || actor != fact.ActorMemberId) ||
            !memberCommand && op.Actor.Kind != "system_process")
            throw new InvalidOperationException("The significant change has another exact native owner, actor or content binding.");
        if (Header is null)
        {
            if (kind != nameof(RecordSignificantChange) || h.Revision != 1 || h.CurrentVersionNumber != 1 || h.Status != "recorded" ||
                fact.ChangeCount != 1 || op.LogicalId != Id || op.OriginalExpectedRevision is not null)
                throw new InvalidOperationException("A significant change begins with its own identified creation.");
            return;
        }
        if (h.ProgramId != Header.ProgramId || kind == nameof(RecordSignificantChange) ||
            h.Revision != Header.Revision + fact.ChangeCount || h.CurrentVersionNumber < Header.CurrentVersionNumber ||
            h.CurrentVersionNumber > Header.CurrentVersionNumber + 1 ||
            fact.ChangeCount == 0 && (h != Header || fact.CurrentContent != CurrentContent) ||
            kind is nameof(ReviseSignificantChange) or nameof(AssessSignificantChange) && op.OriginalExpectedRevision != unchecked((ulong)Header.Revision) ||
            kind is nameof(LinkImpactReviewWork) or nameof(RecordSignificantChangeSystemDescriptionDecision) && op.OriginalExpectedRevision is not null)
            throw new InvalidOperationException("A significant-change transition cannot rewrite identity or its original revision gate.");
    }
    static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.Schema == schema &&
        value.ContentId != Uuid.Empty && value.Bytes > 0 && Hex(value.Sha256);
    static bool Hex(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static Result Fail(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
