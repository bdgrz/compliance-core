using System.Text.Json;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Rules;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;

/// <summary>Owns immutable application identity and compact current successor/progress admission, never lifetime history.</summary>
public sealed class TemplateApplication : Aggregate
{
    public const string Area = "template-applications";
    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _canonicalTemplateId;
    readonly Dictionary<Uuid, MethodologyTemplateAppliedV4> _versionApplications = [];
    readonly Dictionary<Uuid, TemplateApplicationCompleted> _versionCompletions = [];

    public TemplateApplication(Uuid tenantId, Uuid programId, Uuid applicationId, Uuid selectedCommandId = default,
        string? selectedCommandKind = null) : base(applicationId,
        new EventStreamAddress(tenantId.ToString(), Area, applicationId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        Acceptance = new NativeAcceptance(selectedCommandId, selectedCommandKind);
        On<MethodologyTemplateApplied>(ApplyFact);
        On<TemplateSuccessorAdoptionProposed>(ApplyFact);
        On<TemplateSuccessorDispositionRecorded>(ApplyFact);
        On<MethodologyTemplateAppliedV4>(ApplyVersionFact);
        On<TemplateApplicationCompleted>(ApplyCompletionFact);
    }

    public TemplateApplication(Uuid tenantId, Uuid programId, Uuid templateId, bool canonical)
        : this(tenantId, programId, CanonicalIdentity(tenantId, programId, templateId))
    {
        if (!canonical)
            throw new ArgumentException("The canonical constructor requires canonical identity.", nameof(canonical));
        _canonicalTemplateId = templateId;
    }

    public static Uuid CanonicalIdentity(Uuid tenantId, Uuid programId, Uuid templateId)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || templateId == Uuid.Empty)
            throw new ArgumentException("An application requires its tenant, program and template identities.");
        return Uuid.CreateVersion5(tenantId, $"methodology-template-application:{programId}:{templateId}");
    }

    public long LastAppliedVersion { get; private set; }
    public Uuid CurrentApplicationCommandId { get; private set; }
    public bool IsVersionCompleted { get; private set; } = true;
    public MethodologyTemplateAppliedV4? AppliedVersionFor(Uuid commandId) => _versionApplications.GetValueOrDefault(commandId);
    public TemplateApplicationCompleted? CompletionFor(Uuid commandId) => _versionCompletions.GetValueOrDefault(commandId);

    public Result ApplyVersion(Uuid commandId, long version, Uuid actor, NativeFactReference publicationSource,
        ulong publicationPosition, ImmutableContentRef entriesContent, IReadOnlyList<string> create,
        IReadOnlyList<string> propose)
    {
        if (_canonicalTemplateId == Uuid.Empty || Header is not null || commandId == Uuid.Empty || actor == Uuid.Empty ||
            version <= 0 || publicationSource is null || publicationSource.EventId == Uuid.Empty ||
            publicationSource.AggregateVersion == 0 || publicationPosition < publicationSource.AggregateVersion - 1 || publicationSource.Stream != new MethodologyTemplate(_canonicalTemplateId).Stream ||
            !MethodologyTemplateDraft.ValidReference(entriesContent) || !ValidPlan(create, propose))
            return Invalid("Applying a version requires the canonical owner, exact publication and bounded disjoint entry plan.");
        if (_versionApplications.TryGetValue(commandId, out var prior))
            return prior.TemplateVersion == version && prior.AppliedBy == actor && prior.PublicationSource == publicationSource &&
                prior.PublicationPosition == publicationPosition && prior.EntriesContent == entriesContent &&
                prior.EntriesToCreate.SequenceEqual(create) && prior.EntriesToPropose.SequenceEqual(propose)
                ? Result.Success : Conflict("This application command is already bound to another intent.");
        if (LastAppliedVersion == 0 && propose.Count != 0)
            return Invalid("A first application creates drafts and cannot propose revisions to an earlier application.");
        if (!IsVersionCompleted || version <= LastAppliedVersion)
            return Conflict("Apply a later version only after the preceding application completes.");
        RaiseEvent(new MethodologyTemplateAppliedV4(_tenantId, _programId, _canonicalTemplateId, version,
            LastAppliedVersion, commandId, actor, publicationSource, publicationPosition, entriesContent,
            create.ToArray(), propose.ToArray()));
        return Result.Success;
    }

    public Result CompleteVersion(Uuid applicationCommandId, long version, TemplateApplicationOutcomeCounts counts,
        Uuid completionEventId)
    {
        if (_versionCompletions.TryGetValue(applicationCommandId, out var prior))
            return prior.TemplateVersion == version && prior.Counts == counts && prior.CompletionSourceEventId == completionEventId
                ? Result.Success : Conflict("The completion is already bound to another exact version outcome.");
        if (_canonicalTemplateId == Uuid.Empty || CurrentApplicationCommandId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The canonical application is not open yet.", isTransient: true));
        if (IsVersionCompleted || applicationCommandId != CurrentApplicationCommandId || version != LastAppliedVersion)
            return Conflict("Completion must answer the exact in-flight template version.");
        var application = _versionApplications[applicationCommandId];
        if (completionEventId == Uuid.Empty || counts is null || counts.Created < 0 || counts.Proposed < 0 || counts.Refused < 0 ||
            (long)counts.Created + counts.Proposed + counts.Refused != application.EntriesToCreate.Count + application.EntriesToPropose.Count)
            return Invalid("Completion totals must exactly cover the planned entries.");
        RaiseEvent(new TemplateApplicationCompleted(_tenantId, _programId, _canonicalTemplateId, version,
            applicationCommandId, counts, completionEventId));
        return Result.Success;
    }

    void ApplyVersionFact(MethodologyTemplateAppliedV4 fact)
    {
        if (fact.TenantId != _tenantId || fact.ProgramId != _programId || fact.TemplateId != _canonicalTemplateId ||
            Id != CanonicalIdentity(_tenantId, _programId, fact.TemplateId) || Header is not null ||
            fact.CommandId == Uuid.Empty || fact.AppliedBy == Uuid.Empty || fact.TemplateVersion <= LastAppliedVersion ||
            fact.PreviousVersion != LastAppliedVersion || !ValidPlan(fact.EntriesToCreate, fact.EntriesToPropose) ||
            LastAppliedVersion == 0 && fact.EntriesToPropose.Count != 0 ||
            !IsVersionCompleted || _versionApplications.ContainsKey(fact.CommandId) ||
            fact.PublicationSource is null || fact.PublicationSource.EventId == Uuid.Empty || fact.PublicationSource.AggregateVersion == 0 ||
            fact.PublicationPosition < fact.PublicationSource.AggregateVersion - 1 ||
            fact.PublicationSource.Stream != new MethodologyTemplate(fact.TemplateId).Stream ||
            !MethodologyTemplateDraft.ValidReference(fact.EntriesContent))
            throw new InvalidOperationException("A canonical application must advance one complete exact version at a time.");
        _versionApplications.Add(fact.CommandId, fact);
        LastAppliedVersion = fact.TemplateVersion;
        CurrentApplicationCommandId = fact.CommandId;
        IsVersionCompleted = false;
    }

    void ApplyCompletionFact(TemplateApplicationCompleted fact)
    {
        if (fact.TenantId != _tenantId || fact.ProgramId != _programId || fact.TemplateId != _canonicalTemplateId ||
            fact.ApplicationCommandId != CurrentApplicationCommandId || fact.TemplateVersion != LastAppliedVersion ||
            IsVersionCompleted || fact.CompletionSourceEventId == Uuid.Empty || fact.Counts is null ||
            fact.Counts.Created < 0 || fact.Counts.Proposed < 0 || fact.Counts.Refused < 0 ||
            (long)fact.Counts.Created + fact.Counts.Proposed + fact.Counts.Refused !=
                _versionApplications[fact.ApplicationCommandId].EntriesToCreate.Count + _versionApplications[fact.ApplicationCommandId].EntriesToPropose.Count)
            throw new InvalidOperationException("Completion must retain the exact current application and all entry outcomes.");
        _versionCompletions.Add(fact.ApplicationCommandId, fact);
        IsVersionCompleted = true;
    }

    static bool ValidPlan(IReadOnlyList<string>? create, IReadOnlyList<string>? propose) =>
        create is not null && propose is not null && create.Count + propose.Count <= 100 &&
        create.Concat(propose).All(key => key is { Length: > 0 and <= 200 } && key == key.Trim() &&
            key.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')) &&
        create.Concat(propose).Distinct(StringComparer.Ordinal).Count() == create.Count + propose.Count;

    public NativeAcceptance Acceptance { get; }
    public TemplateApplicationHeader? Header { get; private set; }
    public long Revision => Header?.Revision ?? _versionApplications.Count + _versionCompletions.Count;
    public bool IsCreated => Header is not null || LastAppliedVersion > 0;

    public Result Append(DomainEvent value, long preparedRevision, ulong preparedPhysicalPosition,
        TemplateApplicationMutationContent mutation, TemplateApplicationView? before, TemplateApplicationView after)
    {
        if (_canonicalTemplateId != Uuid.Empty || preparedRevision != Revision || preparedPhysicalPosition != CommittedStreamPosition)
            return Conflict("The template application changed while its immutable content was prepared.");
        if (value is not ITemplateApplicationFact fact || fact.Header is null || fact.Content is null || mutation is null || after is null ||
            (Header is null) != (before is null))
            return Invalid("An application append requires its exact prepared immutable change.");
        var rules = new TemplateApplicationRules(_tenantId, _programId, Id, before);
        if (before is not null && (!rules.HasValidCurrent || rules.Header() != Header))
            return Conflict("The application preparation answered another exact authority cut.");
        try
        {
            rules.ApplyChange(mutation, fact.Revision, fact.Header.ActorMemberId, fact.Header.ActorDisplay, fact.OccurredAt);
        }
        catch (InvalidOperationException)
        {
            return Invalid("The prepared application change violates its immutable source or current transition.");
        }
        var preparedAfter = new TemplateApplicationRules(_tenantId, _programId, Id, after);
        if (!preparedAfter.HasValidCurrent || rules.Header() != fact.Header || preparedAfter.Header() != fact.Header ||
            !Matches(fact.Content, mutation) ||
            DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
            return Invalid("The application fact differs from its complete immutable prepared body.");
        var guard = Validate(value);
        if (!guard.IsSuccess)
            return guard;
        RaiseEvent(value);
        return Result.Success;
    }

    Result Validate(DomainEvent value)
    {
        var fact = (ITemplateApplicationFact)value;
        var next = fact.Header;
        var operation = fact.Operation;
        if (_tenantId == Uuid.Empty || Id == Uuid.Empty || next is null ||
            fact.TenantId != _tenantId || fact.SubjectId != Id || next.TenantId != _tenantId ||
            next.ProgramId == Uuid.Empty || next.TemplateApplicationId != Id || fact.Revision != Revision + 1 ||
            next.Revision != fact.Revision || next.ActorMemberId != fact.ActorMemberId || next.ActorMemberId == Uuid.Empty ||
            next.UpdatedAt != fact.OccurredAt || next.UpdatedAt == default || Header is { } prior && next.UpdatedAt < prior.UpdatedAt ||
            next.TemplateId == Uuid.Empty || next.TemplateVersionId == Uuid.Empty || next.TemplateVersionNumber <= 0 ||
            next.DraftCount is < 1 or > 100 || next.CreatedDraftCount < 0 || next.CreatedDraftCount > next.DraftCount ||
            next.Status != (next.CreatedDraftCount == next.DraftCount ? "drafts_created" : "applied") ||
            next.RequestedByMemberId == Uuid.Empty || !Hash(next.TemplateContentSha256) || !Hash(next.ContentSha256) ||
            string.IsNullOrWhiteSpace(next.ApplicationKey) || next.ApplicationKey.Length > 200 ||
            next.ApplicationKey != next.ApplicationKey.Trim() || !next.ApplicationKey.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.') ||
            Id != TemplateApplicationId.For(next.ProgramId, next.TemplateId, next.ApplicationKey) ||
            string.IsNullOrWhiteSpace(next.ActorDisplay) || next.ActorDisplay.Length > 200 ||
            fact.Content.Schema != TemplateApplicationReader.Schema || !Hash(fact.Content.Sha256) || fact.Content.Bytes <= 0 ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1") || operation is null ||
            operation.LogicalId == Uuid.Empty || operation.Owner != Stream || operation.AffectedOwner != Stream ||
            operation.ExpectedRevisionOwner != Stream || operation.RootOperationId != operation.LogicalId ||
            operation.CausationEventId is not null || operation.Episode != Id.ToString() ||
            operation.AcceptedAt != fact.OccurredAt || operation.Actor.Kind != "member" ||
            operation.Actor.Id != next.ActorMemberId.ToString() || operation.Actor.Display != next.ActorDisplay ||
            !Hash(operation.AuthoredIntentSha256))
            return Invalid("The application fact has an invalid exact owner or native binding.");
        if (Header is { } current && (next.TemplateId != current.TemplateId || next.TemplateVersionId != current.TemplateVersionId ||
            next.TemplateVersionNumber != current.TemplateVersionNumber || next.TemplateContentSha256 != current.TemplateContentSha256 ||
            next.ApplicationKey != current.ApplicationKey || next.DraftCount != current.DraftCount ||
            next.RequestedByMemberId != current.RequestedByMemberId))
            return Invalid("An application cannot replace its original exact intent or planned draft set.");
        return value switch
        {
            MethodologyTemplateApplied when Header is null && next.Revision == 1 && next.CreatedDraftCount == 0 &&
                next.RequestedByMemberId == next.ActorMemberId && next.LatestSuccessorVersionId == Uuid.Empty &&
                next.LatestSuccessorVersionNumber == next.TemplateVersionNumber && !next.HasPendingSuccessor &&
                operation.CommandKind == nameof(ApplyMethodologyTemplate) && operation.LogicalId == Id &&
                operation.OriginalExpectedRevision is null => Result.Success,
            TemplateSuccessorAdoptionProposed when Header is { HasPendingSuccessor: false } before &&
                next.CreatedDraftCount == before.CreatedDraftCount && next.HasPendingSuccessor &&
                next.LatestSuccessorVersionId != Uuid.Empty && next.LatestSuccessorVersionId != before.LatestSuccessorVersionId &&
                next.LatestSuccessorVersionNumber > before.LatestSuccessorVersionNumber &&
                operation.CommandKind == nameof(ProposeTemplateSuccessorAdoption) &&
                operation.OriginalExpectedRevision == (ulong)Revision => Result.Success,
            TemplateSuccessorDispositionRecorded when Header is { HasPendingSuccessor: true } before &&
                next.CreatedDraftCount == before.CreatedDraftCount && !next.HasPendingSuccessor &&
                next.LatestSuccessorVersionId == before.LatestSuccessorVersionId &&
                next.LatestSuccessorVersionNumber == before.LatestSuccessorVersionNumber &&
                operation.CommandKind == nameof(RecordTemplateSuccessorDisposition) &&
                operation.OriginalExpectedRevision == (ulong)Revision => Result.Success,
            _ => Invalid("The application fact violates its current admission or successor ordering."),
        };
    }

    void ApplyFact(DomainEvent value)
    {
        var valid = Validate(value);
        if (!valid.IsSuccess)
            throw new InvalidOperationException(valid.Error!.Message);
        var fact = (ITemplateApplicationFact)value;
        Header = fact.Header;
        Acceptance.Observe(value);
    }

    static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null &&
        reference.ContentId != Uuid.Empty && reference.Schema == schema && reference.Bytes > 0 &&
        reference.MediaType == ImmutableContentStoreExtensions.JsonMediaType && Hash(reference.Sha256);

    static bool Matches(ImmutableContentRef reference, TemplateApplicationMutationContent mutation)
    {
        if (!Reference(reference, TemplateApplicationReader.Schema))
            return false;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(mutation, ComplianceCoreJsonContext.Default.TemplateApplicationMutationContent);
        return reference.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }

    static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static Result Invalid(string message) => Result.Failure(new RequestError(RequestErrorKind.Validation, message));
    static Result Conflict(string message) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}
