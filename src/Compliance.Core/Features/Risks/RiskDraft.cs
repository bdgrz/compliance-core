using System.Text.Json;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed class RiskDraft : Aggregate
{
    // Fitz stream events use a u16 frame. Reserve room for the Portia envelope,
    // stream address, metadata, and framing after the serialized domain event.
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    bool _created;
    long _revision;
    string? _identifier;
    Uuid _createRequestId;
    RiskDraftContent? _initialContent;
    Uuid _createActorMemberId;
    string? _createAuthoredIntentSha256;
    readonly Uuid _selectedCommandId;
    RiskDraftRevised? _selectedRevision;

    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid ProgramId { get; private set; }

    public RiskDraft(Uuid tenantId, Uuid riskId, Uuid selectedCommandId = default)
        : base(riskId, new EventStreamAddress(tenantId.ToString(), "risks", riskId.ToString()))
    {
        _tenantId = tenantId;
        _selectedCommandId = selectedCommandId;
        On<RiskDraftCreated>(ev =>
        {
            _created = true;
            _revision = 1;
            ProgramId = ev.ProgramId;
            _identifier = ev.Identifier;
            _createRequestId = ev.CreateRequestId;
            _initialContent = ev.Content;
            _createActorMemberId = ev.ActorMemberId;
            _createAuthoredIntentSha256 = ev.AuthoredIntentSha256;
        });
        On<RiskDraftRevised>(ev =>
        {
            _revision = ev.Revision;
            if (_selectedCommandId != Uuid.Empty && ev.CommandId == _selectedCommandId)
            {
                if (_selectedRevision is not null)
                    throw new InvalidOperationException("A revision command is accepted once on its risk owner.");
                _selectedRevision = ev;
            }
        });
    }

    public static string NormalizeIdentifier(string? identifier) =>
        identifier?.Trim().ToUpperInvariant() ?? string.Empty;

    public static Uuid IdFor(Uuid tenantId, Uuid programId, string identifier) =>
        Uuid.CreateVersion5(Uuid.CreateVersion5(tenantId, programId.ToString()),
            NormalizeIdentifier(identifier));

    /// <summary>Resolves a typed immutable creation before selecting the current program or draft state.</summary>
    public Result<RiskRegistration>? ReplayCreate(CreateRiskDraft request, Uuid actorMemberId)
    {
        if (!_created)
            return null;
        return request.RiskId == Id && request.TenantId == _tenantId && request.ProgramId == ProgramId &&
               _createRequestId == request.RiskId && _createActorMemberId == actorMemberId &&
               _createAuthoredIntentSha256 == AuthoredIntentDigest.Compute(request)
            ? Result<RiskRegistration>.Success(new(Id, _identifier!, 1))
            : Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The risk identity already retains another original creation or a historical unbound creation."));
    }

    public Result<RiskRegistration> Create(CreateRiskDraft request, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (request.RiskId == Uuid.Empty || request.RiskId != Id || request.TenantId != _tenantId ||
            Id != IdFor(_tenantId, request.ProgramId, request.Identifier))
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The authored risk id must match the documented normalized identifier identity."));
        if (ReplayCreate(request, actorMemberId) is { } original)
            return original;
        return Create(request.ProgramId, request.RiskId, request.Identifier,
            new(request.Title, request.Scenario, request.PotentialEffect, request.SourceNote), actorMemberId,
            actorDisplay, changedAt, AuthoredIntentDigest.Compute(request));
    }

    public Result<RiskRegistration> Create(Uuid programId, Uuid createRequestId, string identifier,
        RiskDraftContent content, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt, string? authoredIntentSha256 = null)
    {
        var normalized = NormalizeIdentifier(identifier);
        var error = Validate(normalized, content);
        if (error is not null)
            return Result<RiskRegistration>.Failure(error);
        var clean = Clean(content);
        if (_created)
            return ProgramId == programId && _createRequestId == createRequestId && _createActorMemberId == actorMemberId &&
                   StringComparer.Ordinal.Equals(_identifier, normalized) &&
                   Equal(_initialContent!, clean)
                ? Result<RiskRegistration>.Success(new RiskRegistration(Id, normalized, 1))
                : Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The risk identifier already exists with different draft content."));
        RiskDraftCreated created = new(_tenantId, programId, Id, createRequestId,
            normalized, clean, actorMemberId, actorDisplay, changedAt)
        {
            AuthoredIntentSha256 = authoredIntentSha256,
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(created,
                ComplianceCoreJsonContext.Default.RiskDraftCreated).Length >
            MaximumDraftEventPayloadBytes)
            return Result<RiskRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "The risk draft exceeds the bounded event payload size."));
        RaiseEvent(created);
        return Result<RiskRegistration>.Success(new RiskRegistration(Id, normalized, 1));
    }

    /// <summary>Resolves the selected authored revision before current owner revision validation.</summary>
    public CommandFailure? Revise(ReviseRiskDraft request, Uuid actorMemberId, string actorDisplay,
        DateTimeOffset changedAt)
    {
        if (request.CommandId == Uuid.Empty || request.CommandId != _selectedCommandId ||
            request.TenantId != _tenantId || request.RiskId != Id)
            return CommandFailure.InvalidContent("A revision requires its selected typed command identity and exact owner.");
        var digest = AuthoredIntentDigest.Compute(request);
        if (_selectedRevision is { } original)
            return original.ActorMemberId == actorMemberId && original.ProgramId == request.ProgramId &&
                   original.OriginalExpectedRevision == request.ExpectedRevision && original.AuthoredIntentSha256 == digest
                ? null : CommandFailure.StateConflict("The revision identity already accepted another actor or authored intent.");
        var before = _revision;
        var failure = Revise(request.ProgramId, request.ExpectedRevision,
            new(request.Title, request.Scenario, request.PotentialEffect, request.SourceNote), actorMemberId, actorDisplay,
            changedAt, request.CommandId, digest);
        if (failure is null && _revision != before + 1)
            throw new InvalidOperationException("A new risk revision must append exactly one decision.");
        return failure;
    }

    public CommandFailure? Revise(Uuid programId, long expectedRevision, RiskDraftContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt, Uuid? commandId = null,
        string? authoredIntentSha256 = null)
    {
        if (!_created || ProgramId != programId)
            return CommandFailure.MissingRecord("The risk draft was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("risk draft",
                _revision));
        var error = Validate(_identifier!, content);
        if (error is not null)
            return CommandFailure.InvalidContent(error.Message!);
        RiskDraftRevised revised = new(_tenantId, programId, Id, _revision + 1,
            Clean(content), actorMemberId, actorDisplay, changedAt)
        {
            CommandId = commandId,
            OriginalExpectedRevision = commandId is null ? null : expectedRevision,
            AuthoredIntentSha256 = authoredIntentSha256,
            StoredActor = ActorReference.ForMember(actorMemberId, actorDisplay),
        };
        if (JsonSerializer.SerializeToUtf8Bytes(revised,
                ComplianceCoreJsonContext.Default.RiskDraftRevised).Length >
            MaximumDraftEventPayloadBytes)
            return CommandFailure.InvalidContent("The risk draft exceeds the bounded event payload size.");
        RaiseEvent(revised);
        return null;
    }

    static RequestError? Validate(string identifier, RiskDraftContent? content)
    {
        if (identifier.Length is < 1 or > 80 ||
            !identifier.All(static c => char.IsAsciiLetterUpper(c) ||
                char.IsAsciiDigit(c) || c is '-' or '_' or '.'))
            return new RequestError(RequestErrorKind.Validation,
                "A risk identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        if (content is null ||
            string.IsNullOrWhiteSpace(content.Title) ||
            string.IsNullOrWhiteSpace(content.Scenario) ||
            string.IsNullOrWhiteSpace(content.PotentialEffect))
            return new RequestError(RequestErrorKind.Validation,
                "A risk draft requires a title, scenario, and potential effect.");
        if (content.Title.Length > 200 || content.Scenario.Length > 8000 ||
            content.PotentialEffect.Length > 8000 || content.SourceNote?.Length > 4000)
            return new RequestError(RequestErrorKind.Validation,
                "The risk draft exceeds its content limits.");
        return null;
    }

    static RiskDraftContent Clean(RiskDraftContent content) => new(
        content.Title.Trim(), content.Scenario.Trim(), content.PotentialEffect.Trim(),
        string.IsNullOrWhiteSpace(content.SourceNote) ? null : content.SourceNote.Trim());

    static bool Equal(RiskDraftContent left, RiskDraftContent right) =>
        StringComparer.Ordinal.Equals(left.Title, right.Title) &&
        StringComparer.Ordinal.Equals(left.Scenario, right.Scenario) &&
        StringComparer.Ordinal.Equals(left.PotentialEffect, right.PotentialEffect) &&
        StringComparer.Ordinal.Equals(left.SourceNote, right.SourceNote);
}
