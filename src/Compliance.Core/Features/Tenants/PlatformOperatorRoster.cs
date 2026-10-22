using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>One atomic active roster preserves the last-operator invariant.</summary>
public sealed class PlatformOperatorRoster : Aggregate
{
    static readonly Uuid RosterId = Uuid.Parse(
        "bc279ad5-e9ae-5033-a174-4d1350ce2b07", CultureInfo.InvariantCulture);
    readonly Dictionary<Uuid, Uuid> _operators = [];
    readonly Uuid _selectedCommandId;
    DomainEvent? _selectedFact;
    bool _initialized;

    public PlatformOperatorRoster(Uuid selectedCommandId = default)
        : base(RosterId, new EventStreamAddress("bdgrz", "platform-operator-roster", RosterId.ToString()))
    {
        _selectedCommandId = selectedCommandId;
        On<PlatformOperatorRosterSeeded>(Apply);
        On<PlatformOperatorGranted>(Apply);
        On<PlatformOperatorRevoked>(Apply);
    }

    public static Uuid CreationId => RosterId;
    public bool IsInitialized => _initialized;
    public bool HasSelectedOperation => _selectedFact is not null;
    public bool IsOperator(Uuid userId) => userId != Uuid.Empty && _operators.ContainsKey(userId);
    public Uuid? AssignmentId(Uuid userId) => _operators.TryGetValue(userId, out var id) ? id : null;
    public IReadOnlyList<PlatformOperatorAssignmentView> Operators => _operators
        .OrderBy(static entry => entry.Key.ToString(), StringComparer.Ordinal)
        .Select(static entry => new PlatformOperatorAssignmentView(entry.Key, entry.Value)).ToArray();

    public Result Seed(Uuid rosterId, IEnumerable<Uuid> configuredUserIds, ActorAttribution actor,
        DateTimeOffset occurredAt)
    {
        if (rosterId != RosterId || rosterId != _selectedCommandId || !ValidActor(actor))
            return Failure(RequestErrorKind.Validation, "The fixed roster creation identity and original actor are required.");
        var userIds = CanonicalUsers(configuredUserIds);
        if (userIds.Length == 0 || userIds.Contains(Uuid.Empty))
            return Failure(RequestErrorKind.Validation, "At least one valid bootstrap operator is required.");
        if (_selectedFact is { } prior)
            return prior is PlatformOperatorRosterSeeded seeded && seeded.Actor == actor &&
                seeded.UserIds.SequenceEqual(userIds) ? Result.Success : ChangedIntent();
        if (_initialized || Version != 0)
            return ChangedIntent();
        RaiseEvent(new PlatformOperatorRosterSeeded(rosterId, userIds, actor, occurredAt));
        return Result.Success;
    }

    public Result Grant(Uuid commandId, ulong expectedRevision, Uuid actorUserId, Uuid subjectUserId,
        string reason, ActorAttribution actor, DateTimeOffset occurredAt, string? actorDisplay = null)
    {
        if (ValidateChange(commandId, actorUserId, subjectUserId, reason, actor) is { } error)
            return Result.Failure(error);
        var canonicalReason = reason.Trim();
        if (_selectedFact is { } prior)
            return prior is PlatformOperatorGranted granted && granted.ExpectedRevision == expectedRevision &&
                granted.ActorUserId == actorUserId && granted.SubjectUserId == subjectUserId &&
                granted.Reason == canonicalReason && granted.Actor == actor ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || _operators.ContainsKey(subjectUserId))
            return Failure(RequestErrorKind.Conflict, "The operator roster changed or the user is already assigned.");
        RaiseEvent(new PlatformOperatorGranted(commandId, expectedRevision, actorUserId, subjectUserId,
            actor, occurredAt, canonicalReason, Display(actorUserId, actorDisplay)));
        return Result.Success;
    }

    public Result Revoke(Uuid commandId, ulong expectedRevision, Uuid actorUserId, Uuid subjectUserId,
        Uuid assignmentId, string reason, ActorAttribution actor, DateTimeOffset occurredAt, string? actorDisplay = null)
    {
        if (ValidateChange(commandId, actorUserId, subjectUserId, reason, actor) is { } error)
            return Result.Failure(error);
        if (assignmentId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "An operator assignment identity is required.");
        var canonicalReason = reason.Trim();
        if (_selectedFact is { } prior)
            return prior is PlatformOperatorRevoked revoked && revoked.ExpectedRevision == expectedRevision &&
                revoked.ActorUserId == actorUserId && revoked.SubjectUserId == subjectUserId &&
                revoked.AssignmentId == assignmentId && revoked.Reason == canonicalReason && revoked.Actor == actor
                ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || AssignmentId(subjectUserId) != assignmentId)
            return Failure(RequestErrorKind.Conflict, "The operator roster revision or assignment episode changed.");
        if (_operators.Count == 1)
            return Failure(RequestErrorKind.Conflict, "The last platform operator cannot be revoked.");
        RaiseEvent(new PlatformOperatorRevoked(commandId, expectedRevision, actorUserId, subjectUserId,
            assignmentId, actor, occurredAt, canonicalReason, Display(actorUserId, actorDisplay)));
        return Result.Success;
    }

    RequestError? ValidateChange(Uuid commandId, Uuid actorUserId, Uuid subjectUserId, string reason,
        ActorAttribution actor)
    {
        if (!_initialized || !IsOperator(actorUserId))
            return new RequestError(RequestErrorKind.Forbidden, "The actor is not a platform operator.");
        if (commandId == Uuid.Empty || commandId != _selectedCommandId || !ValidActor(actor))
            return new RequestError(RequestErrorKind.Validation, "A selected command identity and original actor are required.");
        if (subjectUserId == Uuid.Empty)
            return new RequestError(RequestErrorKind.Validation, "A platform user is required.");
        return ValidReason(reason) ? null :
            new RequestError(RequestErrorKind.Validation, "A reason of at most 500 characters is required.");
    }

    void Apply(PlatformOperatorRosterSeeded seeded)
    {
        if (_initialized || Version != 0 || seeded.RosterId != RosterId || !ValidActor(seeded.Actor) ||
            seeded.UserIds.Length == 0 || seeded.UserIds.Contains(Uuid.Empty) ||
            !seeded.UserIds.SequenceEqual(CanonicalUsers(seeded.UserIds)))
            throw new InvalidOperationException("The platform operator roster seed is invalid.");
        Select(seeded.RosterId, seeded);
        _initialized = true;
        foreach (var userId in seeded.UserIds)
            _operators.Add(userId, Uuid.CreateVersion5(RosterId, $"platform-operator:initial:{userId}"));
    }

    void Apply(PlatformOperatorGranted granted)
    {
        ValidateFact(granted.CommandId, granted.ExpectedRevision, granted.ActorUserId, granted.SubjectUserId,
            granted.Reason, granted.Actor);
        if (_operators.ContainsKey(granted.SubjectUserId))
            throw new InvalidOperationException("An active operator cannot be granted another assignment.");
        Select(granted.CommandId, granted);
        _operators.Add(granted.SubjectUserId, granted.CommandId);
    }

    void Apply(PlatformOperatorRevoked revoked)
    {
        ValidateFact(revoked.CommandId, revoked.ExpectedRevision, revoked.ActorUserId, revoked.SubjectUserId,
            revoked.Reason, revoked.Actor);
        if (revoked.AssignmentId == Uuid.Empty || AssignmentId(revoked.SubjectUserId) != revoked.AssignmentId ||
            _operators.Count == 1)
            throw new InvalidOperationException("Operator revocation must close the exact assignment and retain an operator.");
        Select(revoked.CommandId, revoked);
        _operators.Remove(revoked.SubjectUserId);
    }

    void ValidateFact(Uuid commandId, ulong expectedRevision, Uuid actorUserId, Uuid subjectUserId,
        string reason, ActorAttribution actor)
    {
        if (!_initialized || commandId == Uuid.Empty || expectedRevision != Version || !IsOperator(actorUserId) ||
            subjectUserId == Uuid.Empty || !ValidReason(reason) || reason != reason.Trim() || !ValidActor(actor))
            throw new InvalidOperationException("An operator fact must retain its actor, subject, reason and original revision.");
    }

    void Select(Uuid commandId, DomainEvent fact)
    {
        if (commandId != _selectedCommandId)
            return;
        if (_selectedFact is not null)
            throw new InvalidOperationException("One operator operation cannot be accepted twice.");
        _selectedFact = fact;
    }

    static bool ValidActor(ActorAttribution actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Subject) && !string.IsNullOrWhiteSpace(actor.Issuer);
    static bool ValidReason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    static Uuid[] CanonicalUsers(IEnumerable<Uuid> users) => users.Distinct()
        .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
    static string Display(Uuid userId, string? display) => string.IsNullOrWhiteSpace(display) ? userId.ToString() : display.Trim();
    static Result ChangedIntent() => Failure(RequestErrorKind.Conflict, "This operator operation already has different accepted intent.");
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
