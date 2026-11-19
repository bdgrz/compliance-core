using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>One tenant's maximum two seats, including pending designations. Only completed seats permit release.</summary>
public sealed class PrimaryOrgAdmins : Aggregate
{
    readonly Uuid _tenantId;
    readonly Uuid _selectedCommandId;
    readonly Dictionary<Uuid, PrimaryOrgAdminSeat> _seats = [];
    readonly Dictionary<Uuid, PrimaryOrgAdminDesignationRequested> _requests = [];
    readonly Dictionary<Uuid, PrimaryOrgAdminReleased> _releases = [];
    readonly Dictionary<Uuid, Outcome> _outcomes = [];

    public bool HasDesignation(Uuid designationId) => _requests.ContainsKey(designationId);

    public int SeatCount => _seats.Count;
    public int DesignatedCount => _seats.Values.Count(static seat => seat.IsDesignated);
    public IReadOnlyList<PrimaryOrgAdminSeat> Seats => _seats.Values
        .OrderBy(static seat => seat.UserId.ToString(), StringComparer.Ordinal).ToArray();

    public bool HasDesignated(Uuid userId, Uuid episodeId, Uuid designationId) =>
        _seats.TryGetValue(userId, out var seat) && seat.IsDesignated &&
        seat.MembershipEpisodeId == episodeId && seat.DesignationId == designationId;

    public bool HasPending(Uuid userId, Uuid episodeId, Uuid designationId) =>
        _seats.TryGetValue(userId, out var seat) && !seat.IsDesignated &&
        seat.MembershipEpisodeId == episodeId && seat.DesignationId == designationId;

    public bool HasReleased(Uuid userId, Uuid episodeId, Uuid designationId) =>
        _releases.Values.Any(release => release.UserId == userId &&
            release.MembershipEpisodeId == episodeId && release.DesignationId == designationId);

    public PrimaryOrgAdmins(Uuid tenantId, Uuid selectedCommandId = default) : base(tenantId,
        new EventStreamAddress(tenantId.ToString(), "primary-org-admins", tenantId.ToString()))
    {
        _tenantId = tenantId;
        _selectedCommandId = selectedCommandId;
        On<PrimaryOrgAdminDesignationRequested>(ev =>
        {
            RequireIdentity(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId);
            if (!ValidActor(ev.RequestedBy) || _seats.Count >= 2 || _seats.ContainsKey(ev.UserId) ||
                _requests.ContainsKey(ev.DesignationId) || _releases.ContainsKey(ev.DesignationId))
                throw new InvalidOperationException("A primary designation must reserve one available seat under a new identity.");
            _seats.Add(ev.UserId, new(ev.UserId, ev.MembershipEpisodeId, ev.DesignationId, false));
            _requests.Add(ev.DesignationId, ev);
        });
        On<PrimaryOrgAdminDesignated>(ev =>
        {
            RequirePending(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId);
            _seats[ev.UserId] = new(ev.UserId, ev.MembershipEpisodeId, ev.DesignationId, true);
            _outcomes.Add(ev.DesignationId, new(ev.UserId, ev.MembershipEpisodeId, null));
        });
        On<PrimaryOrgAdminDesignationRefused>(ev =>
        {
            RequirePending(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId);
            if (ev.Reason is not ("not_active" or "guest"))
                throw new InvalidOperationException("A primary designation refusal must retain the member's outcome.");
            _seats.Remove(ev.UserId);
            _outcomes.Add(ev.DesignationId, new(ev.UserId, ev.MembershipEpisodeId, ev.Reason));
        });
        On<PrimaryOrgAdminReleased>(ev =>
        {
            RequireIdentity(ev.TenantId, ev.UserId, ev.MembershipEpisodeId, ev.DesignationId);
            if (!HasDesignated(ev.UserId, ev.MembershipEpisodeId, ev.DesignationId) ||
                !_seats.TryGetValue(ev.ReplacedByUserId, out var replacement) || !replacement.IsDesignated ||
                ev.UserId == ev.ReplacedByUserId || ev.CommandId == Uuid.Empty ||
                _requests.ContainsKey(ev.CommandId) || _releases.ContainsKey(ev.CommandId) ||
                !ValidReason(ev.Reason) || !ValidActor(ev.ReleasedBy))
                throw new InvalidOperationException("A primary release requires another completed primary and its own accepted identity.");
            _seats.Remove(ev.UserId);
            _releases.Add(ev.CommandId, ev);
        });
    }

    public Result Designate(Uuid designationId, Uuid userId, Uuid episodeId, ActorAttribution? requestedBy = null)
    {
        var actor = requestedBy ?? new ActorAttribution("system", "system");
        if (!ValidIdentity(designationId, userId, episodeId) || !Selected(designationId) || !ValidActor(actor))
            return Failure(RequestErrorKind.Validation, "The tenant, member episode, selected designation and original actor are required.");
        if (_requests.TryGetValue(designationId, out var prior))
            return prior.UserId == userId && prior.MembershipEpisodeId == episodeId && prior.RequestedBy == actor
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The designation identity already has different intent.");
        if (_releases.ContainsKey(designationId))
            return Failure(RequestErrorKind.Conflict, "The command identity is already used for a release.");
        if (_seats.TryGetValue(userId, out var current))
            return current.MembershipEpisodeId == episodeId ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The primary seat belongs to another membership episode.");
        if (_seats.Count >= 2)
            return Failure(RequestErrorKind.Conflict, "An organization can have at most two primary administrators, including pending designations.");
        RaiseEvent(new PrimaryOrgAdminDesignationRequested(_tenantId, userId, episodeId, designationId, actor));
        return Result.Success;
    }

    public Result RecordOutcome(Uuid designationId, Uuid userId, Uuid episodeId, string? refusal)
    {
        if (!ValidIdentity(designationId, userId, episodeId) || refusal is not (null or "not_active" or "guest"))
            return Failure(RequestErrorKind.Validation, "The exact designation, member episode and member outcome are required.");
        if (_outcomes.TryGetValue(designationId, out var prior))
            return prior == new Outcome(userId, episodeId, refusal) ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The designation already has another outcome.");
        if (!HasPending(userId, episodeId, designationId))
            return Failure(RequestErrorKind.Conflict, "There is no matching pending designation.");
        if (refusal is null)
            RaiseEvent(new PrimaryOrgAdminDesignated(_tenantId, userId, episodeId, designationId));
        else
            RaiseEvent(new PrimaryOrgAdminDesignationRefused(_tenantId, userId, episodeId, designationId, refusal));
        return Result.Success;
    }

    public Result Release(Uuid commandId, Uuid userId, Uuid designationId, string reason, ActorAttribution? releasedBy = null)
    {
        var actor = releasedBy ?? new ActorAttribution("system", "system");
        if (commandId == Uuid.Empty || _tenantId == Uuid.Empty || userId == Uuid.Empty || designationId == Uuid.Empty ||
            !Selected(commandId) || !ValidReason(reason) || !ValidActor(actor))
            return Failure(RequestErrorKind.Validation, "The selected command, designation, original actor and release reason of at most 1000 characters are required.");
        if (_releases.TryGetValue(commandId, out var prior))
            return prior.UserId == userId && prior.DesignationId == designationId && prior.Reason == reason.Trim() && prior.ReleasedBy == actor
                ? Result.Success : Failure(RequestErrorKind.Conflict, "The release identity already has different intent.");
        if (_requests.ContainsKey(commandId))
            return Failure(RequestErrorKind.Conflict, "The command identity is already used for a designation.");
        if (!_seats.TryGetValue(userId, out var seat) || !seat.IsDesignated || seat.DesignationId != designationId)
            return Failure(RequestErrorKind.Conflict, "The exact completed primary designation is required.");
        var replacement = _seats.Values.FirstOrDefault(other => other.UserId != userId && other.IsDesignated);
        if (replacement is null)
            return Failure(RequestErrorKind.Conflict, "Designate a replacement primary administrator before releasing this one.");
        RaiseEvent(new PrimaryOrgAdminReleased(_tenantId, userId, seat.MembershipEpisodeId, designationId,
            commandId, replacement.UserId, reason.Trim(), actor));
        return Result.Success;
    }

    bool Selected(Uuid commandId) => _selectedCommandId == Uuid.Empty || _selectedCommandId == commandId;

    bool ValidIdentity(Uuid designationId, Uuid userId, Uuid episodeId) => _tenantId != Uuid.Empty &&
        designationId != Uuid.Empty && userId != Uuid.Empty && episodeId != Uuid.Empty;

    void RequireIdentity(Uuid tenantId, Uuid userId, Uuid episodeId, Uuid designationId)
    {
        if (tenantId != _tenantId || !ValidIdentity(designationId, userId, episodeId))
            throw new InvalidOperationException("A primary administrator fact must retain the exact tenant, user and membership episode.");
    }

    void RequirePending(Uuid tenantId, Uuid userId, Uuid episodeId, Uuid designationId)
    {
        RequireIdentity(tenantId, userId, episodeId, designationId);
        if (!HasPending(userId, episodeId, designationId) || _outcomes.ContainsKey(designationId))
            throw new InvalidOperationException("A primary designation outcome must answer its exact pending reservation once.");
    }

    static bool ValidReason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 1000;
    static bool ValidActor(ActorAttribution actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Subject) && !string.IsNullOrWhiteSpace(actor.Issuer);
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
    sealed record Outcome(Uuid UserId, Uuid MembershipEpisodeId, string? Refusal);
}
