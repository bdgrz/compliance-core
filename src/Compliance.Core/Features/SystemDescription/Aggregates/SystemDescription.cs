using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Contracts;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Events;
namespace Bdgrz.Compliance.Features.SystemDescription.Aggregates;

/// <summary>Bounded native description authority. Narrative content, source manifests and history are immutable references.</summary>
public sealed class SystemDescription : Aggregate
{
    public const string Area = "system-descriptions";
    readonly Uuid _tenantId;
    readonly Uuid _requestedProgramId;
    public SystemDescription(Uuid tenantId, Uuid programId, Uuid descriptionId, Uuid selectedCommandId = default)
        : base(descriptionId, new EventStreamAddress(tenantId.ToString(), Area, descriptionId.ToString()))
    {
        _tenantId = tenantId;
        _requestedProgramId = programId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<SystemDescriptionDraftCreated>(ApplyFact);
        On<SystemDescriptionDraftRevised>(ApplyFact);
        On<SystemDescriptionSourcesBound>(ApplyFact);
        On<SystemDescriptionReviewed>(ApplyFact);
        On<SystemDescriptionApproved>(ApplyFact);
        On<SystemDescriptionSuccessorProposed>(ApplyFact);
        On<SystemDescriptionNoChangeRecorded>(ApplyFact);
        On<SystemDescriptionBound>(ApplyFact);
    }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => Header?.ProgramId ?? _requestedProgramId;
    public long Revision => Header?.Revision ?? 0;
    public long NativeSequence { get; private set; }
    public SystemDescriptionAuthorityHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentVersionContent { get; private set; }
    public ImmutableContentRef? CurrentAuthorityContent { get; private set; }
    public ImmutableContentRef? SourceSnapshotContent { get; private set; }
    public NativeAcceptance Acceptance { get; }
    public Result Append(DomainEvent value, FrozenSystemDescriptionMutation prepared, long sequence, ulong physicalPosition)
    {
        if (NativeSequence != sequence || CommittedStreamPosition != physicalPosition)
            return Failure("The description changed while its exact source and narrative were prepared.", true);
        if (value is not ISystemDescriptionFact fact || fact.Header != prepared.Header || fact.CurrentVersionContent != prepared.CurrentVersion ||
            SystemDescriptionContent.Payload(fact) != prepared.Payload || fact.CurrentAuthorityContent != prepared.AuthorityContent || fact.SourceSnapshotContent != prepared.SourceSnapshotContent || fact.ChangeCount != (prepared.Changed ? 1 : 0) ||
            (Header is null ? prepared.Before.Revision != 0 || prepared.Header.ProgramId != _requestedProgramId :
                prepared.Before.Revision == 0 || prepared.BeforeAuthority is null || CurrentAuthorityContent is null ||
                !SystemDescriptionAuthority.Matches(CurrentAuthorityContent, prepared.BeforeAuthority, SystemDescriptionAuthority.Schema) ||
                SystemDescriptionAuthority.Header(prepared.Before, prepared.BeforeAuthority) != Header) ||
            (prepared.Changed || prepared.Mutation is SystemDescriptionReviewedBody or SystemDescriptionNoChangeRecordedBody) &&
                SystemDescriptionContent.Actor(prepared.Mutation) != fact.ActorMemberId ||
            !SystemDescriptionContent.Verify(prepared))
            return Failure("The description mutation does not carry its exact verified local rule transition.");
        try
        {
            Validate(value, fact);
            if (DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
                return Failure("The compact native description fact exceeds the bounded event envelope.");
        }
        catch (InvalidOperationException)
        {
            return Failure("The description mutation has an invalid native authority binding.");
        }
        RaiseEvent(value);
        return Result.Success;
    }
    void ApplyFact(DomainEvent value)
    {
        var fact = (ISystemDescriptionFact)value;
        Validate(value, fact);
        Header = fact.Header;
        CurrentVersionContent = fact.CurrentVersionContent;
        CurrentAuthorityContent = fact.CurrentAuthorityContent;
        NativeSequence = fact.NativeSequence;
        SourceSnapshotContent = fact.SourceSnapshotContent;
        Acceptance.Observe(value);
    }
    void Validate(DomainEvent value, ISystemDescriptionFact fact)
    {
        var h = fact.Header;
        var op = fact.Operation;
        if (h is null || op is null || op.Actor is null)
            throw new InvalidOperationException("A description fact requires its complete native authority and operation.");
        OwnerProvenance.RequireOwn(this, op.LogicalId, op);
        var kind = value switch
        {
            SystemDescriptionDraftCreated => nameof(CreateSystemDescriptionDraft),
            SystemDescriptionDraftRevised => nameof(ReviseSystemDescriptionDraft),
            SystemDescriptionSourcesBound => nameof(BindSystemDescriptionSources),
            SystemDescriptionReviewed => nameof(ReviewSystemDescription),
            SystemDescriptionApproved => nameof(ApproveSystemDescription),
            SystemDescriptionSuccessorProposed => nameof(ProposeSystemDescriptionSuccessor),
            SystemDescriptionNoChangeRecorded => nameof(RecordSystemDescriptionNoChangeDecision),
            SystemDescriptionBound => nameof(BindSystemDescriptionToExamination),
            _ => "",
        };
        if (fact.TenantId != _tenantId || h.TenantId != _tenantId || h.ProgramId == Uuid.Empty || fact.ProgramId != h.ProgramId ||
            h.SystemDescriptionId != Id || fact.SystemDescriptionId != Id || fact.AggregateRevision != h.Revision || h.Revision <= 0 ||
            h.CurrentVersion < 1 || h.CurrentVersionId == Uuid.Empty || h.AuthorMemberId == Uuid.Empty ||
            h.Status is not ("draft" or "approved") || string.IsNullOrWhiteSpace(h.Title) || h.Title.Length > 200 ||
            !Hex(h.ContentSha256) || !Hex(h.NoChangeDecisionsSha256) || !Hex(h.BindingsSha256) || !Hex(h.VersionsSha256) ||
            !Hex(h.ReviewsSha256) || !Hex(h.ApprovalsSha256) || h.VersionUpdatedAt == default ||
            fact.NativeSequence != NativeSequence + 1 || fact.ChangeCount is < 0 or > 1 || fact.ActorMemberId == Uuid.Empty ||
            op.Actor.Kind != "member" || !Uuid.TryParse(op.Actor.Id, out var member) || member != fact.ActorMemberId ||
            op.CommandKind != kind || op.Owner != Stream || op.FirstOwner != Stream || op.AffectedOwner != Stream ||
            op.ExpectedRevisionOwner != Stream || op.Episode != Id.ToString() || op.RootOperationId != op.LogicalId ||
            op.CausationEventId is not null || !Hex(op.AuthoredIntentSha256) || op.AcceptedAt == default || fact.OccurredAt != op.AcceptedAt ||
            !Reference(fact.CurrentVersionContent, SystemDescriptionContent.CurrentSchema) ||
            !Reference(fact.CurrentAuthorityContent, SystemDescriptionAuthority.Schema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1") ||
            SystemDescriptionContent.Payload(fact) is not { Bytes: > 0 } payload || payload.ContentId == Uuid.Empty || !Hex(payload.Sha256))
            throw new InvalidOperationException("The description native fact has another exact scope, actor, result or content binding.");
        if (Header is null)
        {
            if (kind != nameof(CreateSystemDescriptionDraft) || op.LogicalId != Id || h.Revision != 1 || h.CurrentVersion != 1 ||
                h.ParentVersionId is not null || h.Status != "draft" || fact.ChangeCount != 1 || op.OriginalExpectedRevision is not null)
                throw new InvalidOperationException("A description begins with its own identified initial narrative.");
            return;
        }
        var before = Header;
        var nextVersion = value is SystemDescriptionDraftRevised or SystemDescriptionSourcesBound or SystemDescriptionSuccessorProposed;
        if (value is SystemDescriptionDraftCreated || h.ProgramId != before.ProgramId || h.Revision != before.Revision + fact.ChangeCount ||
            h.CurrentVersion != before.CurrentVersion + (nextVersion ? 1 : 0) ||
            nextVersion && (h.ParentVersionId != before.CurrentVersionId || h.CurrentVersionId == before.CurrentVersionId || h.Status != "draft") ||
            !nextVersion && h.CurrentVersionId != before.CurrentVersionId ||
            op.OriginalExpectedRevision != unchecked((ulong)before.Revision) ||
            fact.ChangeCount == 0 && (h != before || fact.CurrentVersionContent != CurrentVersionContent))
            throw new InvalidOperationException("A description transition cannot rewrite identity, lineage or its caller's original revision gate.");
    }
    static bool Reference(ImmutableContentRef? value, string schema) => value is { Bytes: > 0 } && value.ContentId != Uuid.Empty &&
        value.Schema == schema && Hex(value.Sha256);
    static bool Hex(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static Result Failure(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
