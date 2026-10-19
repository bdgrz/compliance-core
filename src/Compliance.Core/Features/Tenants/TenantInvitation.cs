using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>One replaceable, single-use invitation per tenant and normalized email address.</summary>
public sealed class TenantInvitation : Aggregate
{
    static readonly Uuid InvitationNamespaceId = Uuid.Parse(
        "d769ee6a-18e9-529e-a5a5-46082e96db53", CultureInfo.InvariantCulture);

    readonly Uuid _tenantId;
    readonly string _emailAddress;
    string? _tokenHash;
    DateTimeOffset _expiresAt;
    string? _affiliation;
    Uuid _invitedBy;
    string? _invitedByDisplay;
    DateTimeOffset _invitedAt;
    bool _administrator;
    string? _builtInRole;
    Uuid[]? _guestProgramIds;
    DateOnly? _guestExpiresOn;
    Uuid? _deliveryAttemptId;
    string _deliveryStatus = "pending";
    bool _created;
    bool _accepted;
    Uuid _acceptedUserId;

    public TenantInvitation(Uuid tenantId, string emailAddress)
        : base(CreateId(tenantId, emailAddress),
            new EventStreamAddress(tenantId.ToString(), "tenant-invitations",
                CreateId(tenantId, emailAddress).ToString()))
    {
        _tenantId = tenantId;
        _emailAddress = Normalize(emailAddress);
        On<TenantMemberInvited>(Apply);
        On<TenantInvitationAccepted>(Apply);
        On<TenantInvitationDeliverySent>(sent =>
        {
            if (_deliveryAttemptId != sent.DeliveryAttemptId)
                throw new InvalidOperationException("The invitation delivery outcome is stale.");
            _deliveryStatus = "delivered";
        });
        On<TenantInvitationDeliveryFailed>(failed =>
        {
            if (_deliveryAttemptId != failed.DeliveryAttemptId)
                throw new InvalidOperationException("The invitation delivery outcome is stale.");
            _deliveryStatus = "failed";
        });
    }

    public bool IsAccepted => _accepted;
    public static Uuid IdFor(Uuid tenantId, string emailAddress) =>
        CreateId(tenantId, emailAddress);
    public Uuid? CurrentDeliveryAttemptId => _deliveryAttemptId;
    public string DeliveryStatus => _deliveryStatus;

    public static Uuid DeliveryAttemptFor(TenantMemberInvited invited) =>
        invited.DeliveryAttemptId ?? Uuid.CreateVersion5(InvitationNamespaceId,
            $"legacy-delivery\n{invited.TenantId}\n{invited.EmailAddress}\n{invited.TokenHash}");

    public Result Invite(string affiliation, bool administrator, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, Uuid invitedBy,
        string? builtInRole = null, Uuid? deliveryAttemptId = null,
        string? tokenKeyId = null)
    {
        return InviteCore(affiliation, administrator, tokenHash, expiresAt, now, invitedBy,
            builtInRole, deliveryAttemptId, tokenKeyId, null, null, null);
    }

    public Result InviteGuest(IReadOnlyList<Uuid> programIds, DateOnly expiresOn,
        string tokenHash, DateTimeOffset invitationExpiresAt, DateTimeOffset now, Uuid invitedBy,
        Uuid? deliveryAttemptId = null, string? tokenKeyId = null,
        string? invitedByDisplay = null)
    {
        if (programIds is null || programIds.Count == 0 || programIds.Any(id => id == Uuid.Empty) ||
            programIds.Distinct().Count() != programIds.Count ||
            expiresOn <= DateOnly.FromDateTime(now.UtcDateTime))
            return Failure(RequestErrorKind.Validation,
                "A guest invitation requires distinct program scopes and a future membership end date.");

        var normalizedPrograms = programIds.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        return InviteCore("guest", false, tokenHash, invitationExpiresAt, now, invitedBy,
            null, deliveryAttemptId, tokenKeyId, normalizedPrograms, expiresOn, invitedByDisplay);
    }

    Result InviteCore(string affiliation, bool administrator, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, Uuid invitedBy, string? builtInRole,
        Uuid? deliveryAttemptId, string? tokenKeyId, Uuid[]? guestProgramIds,
        DateOnly? guestExpiresOn, string? invitedByDisplay)
    {
        var isGuest = affiliation == "guest";
        if (affiliation is not ("client_personnel" or "firm_staff" or "guest"))
            return Failure(RequestErrorKind.Validation,
                "Affiliation must be client_personnel, firm_staff, or guest.");
        if (isGuest && (administrator || builtInRole is not null || guestProgramIds is not { Length: > 0 } ||
                        guestProgramIds.Any(id => id == Uuid.Empty) ||
                        guestProgramIds.Distinct().Count() != guestProgramIds.Length ||
                        guestExpiresOn is not { } endDate ||
                        endDate <= DateOnly.FromDateTime(now.UtcDateTime) ||
                        endDate == DateOnly.MaxValue || string.IsNullOrWhiteSpace(invitedByDisplay)))
            return Failure(RequestErrorKind.Validation,
                "A guest invitation requires scoped programs, a future membership end date, and no administrator or built-in role.");
        if (!isGuest && (guestProgramIds is not null || guestExpiresOn is not null))
            return Failure(RequestErrorKind.Validation,
                "Only a guest invitation may include guest program scopes or an end date.");
        if (administrator && affiliation != "client_personnel")
            return Failure(RequestErrorKind.Validation, "The first administrator must be client personnel.");
        if (administrator && builtInRole is not null)
            return Failure(RequestErrorKind.Validation,
                "A first-administrator invitation cannot select a second role.");
        if (builtInRole is not null &&
            (affiliation != "client_personnel" || BuiltInRbac.TeamIdForRole(_tenantId, builtInRole) is null))
            return Failure(RequestErrorKind.Validation,
                "A built-in role requires client personnel and a supported role value.");
        if (_accepted)
            return Failure(RequestErrorKind.Conflict, "The invitation has already been accepted.");
        if (_affiliation is not null &&
            (_affiliation != affiliation || _administrator != administrator))
            return Failure(RequestErrorKind.Conflict,
                "The pending invitation has a different affiliation or administrator purpose.");
        if (!IsTokenHash(tokenHash) || expiresAt <= now || invitedBy == Uuid.Empty ||
            now == default)
            return Failure(RequestErrorKind.Validation, "A valid invitation is required.");

        deliveryAttemptId ??= Uuid.CreateVersion4();
        RaiseEvent(new TenantMemberInvited(_tenantId, _emailAddress, affiliation,
            administrator, tokenHash, expiresAt, invitedBy, builtInRole, deliveryAttemptId,
            tokenKeyId, guestProgramIds?.ToArray(), guestExpiresOn, now,
            invitedByDisplay?.Trim()));
        return Result.Success;
    }

    public Result RecordDeliverySent(Uuid deliveryAttemptId, DateTimeOffset sentAt)
    {
        var check = CheckDeliveryAttempt(deliveryAttemptId, sentAt);
        if (check is not null)
            return Result.Failure(check);
        if (_deliveryStatus == "delivered")
            return Result.Success;
        if (_deliveryStatus == "failed")
            return Failure(RequestErrorKind.Conflict,
                "The failed invitation delivery attempt must be reissued.");
        RaiseEvent(new TenantInvitationDeliverySent(_tenantId, _emailAddress,
            deliveryAttemptId, sentAt));
        return Result.Success;
    }

    public Result RecordDeliveryFailure(Uuid deliveryAttemptId, string failureCode,
        DateTimeOffset failedAt)
    {
        var check = CheckDeliveryAttempt(deliveryAttemptId, failedAt);
        if (check is not null)
            return Result.Failure(check);
        if (string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > 100)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A delivery failure code is required and must be at most 100 characters."));
        if (_deliveryStatus == "failed")
            return Result.Success;
        if (_deliveryStatus == "delivered")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The invitation delivery attempt has already succeeded."));
        RaiseEvent(new TenantInvitationDeliveryFailed(_tenantId, _emailAddress,
            deliveryAttemptId, failureCode.Trim(), failedAt));
        return Result.Success;
    }

    public Result Accept(Uuid userId, string token, DateTimeOffset now)
    {
        if (_accepted)
            return _acceptedUserId == userId
                ? Result.Success
                : Failure(RequestErrorKind.Forbidden, "The invitation belongs to another user.");
        if (userId == Uuid.Empty || string.IsNullOrWhiteSpace(token) || _tokenHash is null)
            return Failure(RequestErrorKind.Validation, "A valid invitation token is required.");
        if (now >= _expiresAt)
            return Failure(RequestErrorKind.Validation, "The invitation has expired.");
        if (_invitedAt != default && now < _invitedAt)
            return Failure(RequestErrorKind.Validation,
                "An invitation cannot be accepted before it was issued.");
        if (_affiliation == "guest" && _guestExpiresOn is { } guestExpiresOn &&
            DateOnly.FromDateTime(now.UtcDateTime) > guestExpiresOn)
            return Failure(RequestErrorKind.Validation, "The guest membership has expired.");

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expectedHash = Convert.FromHexString(_tokenHash);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
            return Failure(RequestErrorKind.Validation, "The invitation token is invalid.");

        RaiseEvent(new TenantInvitationAccepted(_tenantId, userId, _emailAddress,
            _affiliation!, _administrator, _builtInRole, _guestProgramIds?.ToArray(),
            _guestExpiresOn, now, _invitedBy, _invitedByDisplay));
        return Result.Success;
    }

    void Apply(TenantMemberInvited invited)
    {
        if (invited.TenantId != _tenantId || invited.EmailAddress != _emailAddress)
            throw new InvalidOperationException("The invitation does not match its tenant and email address.");
        if (invited.Affiliation is not ("client_personnel" or "firm_staff" or "guest") ||
            !IsTokenHash(invited.TokenHash) || invited.ExpiresAt == default ||
            invited.InvitedBy == Uuid.Empty ||
            invited.InvitedAt != default && invited.ExpiresAt <= invited.InvitedAt ||
            invited.Administrator && invited.Affiliation != "client_personnel" ||
            invited.BuiltInRole is not null &&
            (invited.Affiliation != "client_personnel" ||
             BuiltInRbac.TeamIdForRole(_tenantId, invited.BuiltInRole) is null))
            throw new InvalidOperationException("The invitation event has invalid terms.");
        var isGuest = invited.Affiliation == "guest";
        if (isGuest && (invited.Administrator || invited.BuiltInRole is not null ||
                        invited.InvitedAt == default ||
                        invited.GuestProgramIds is not { Count: > 0 } programs ||
                        programs.Any(id => id == Uuid.Empty) ||
                        programs.Distinct().Count() != programs.Count ||
                        invited.GuestExpiresOn is not { } expiresOn ||
                        expiresOn <= DateOnly.FromDateTime(invited.InvitedAt.UtcDateTime) ||
                        expiresOn == DateOnly.MaxValue ||
                        invited.InvitedBy == Uuid.Empty ||
                        string.IsNullOrWhiteSpace(invited.InvitedByDisplay)))
            throw new InvalidOperationException("The guest invitation event has invalid scope or expiry terms.");
        if (!isGuest && (invited.GuestProgramIds is not null || invited.GuestExpiresOn is not null))
            throw new InvalidOperationException("Only a guest invitation event may carry guest scope terms.");
        _created = true;
        _tokenHash = invited.TokenHash;
        _expiresAt = invited.ExpiresAt;
        _affiliation = invited.Affiliation;
        _invitedBy = invited.InvitedBy;
        _invitedByDisplay = invited.InvitedByDisplay?.Trim();
        _invitedAt = invited.InvitedAt;
        _administrator = invited.Administrator;
        _builtInRole = invited.BuiltInRole;
        _guestProgramIds = invited.GuestProgramIds?.ToArray();
        _guestExpiresOn = invited.GuestExpiresOn;
        _deliveryAttemptId = DeliveryAttemptFor(invited);
        _deliveryStatus = "pending";
    }

    void Apply(TenantInvitationAccepted accepted)
    {
        if (!_created || _accepted || accepted.TenantId != _tenantId ||
            accepted.EmailAddress != _emailAddress ||
            accepted.UserId == Uuid.Empty || accepted.Affiliation != _affiliation ||
            accepted.Administrator != _administrator || accepted.BuiltInRole != _builtInRole)
            throw new InvalidOperationException("The accepted invitation does not match its pending terms.");
        if (accepted.AcceptedAt != default && accepted.AcceptedAt >= _expiresAt)
            throw new InvalidOperationException("An expired invitation cannot be accepted.");
        if (accepted.AcceptedAt != default && _invitedAt != default &&
            accepted.AcceptedAt < _invitedAt)
            throw new InvalidOperationException("An invitation cannot be accepted before it was issued.");
        if (_affiliation == "guest" &&
            (accepted.AcceptedAt == default || accepted.GuestExpiresOn != _guestExpiresOn ||
             accepted.AcceptedAt < _invitedAt ||
             accepted.GuestProgramIds is null || !_guestProgramIds!.SequenceEqual(accepted.GuestProgramIds) ||
             accepted.InvitedBy != _invitedBy || accepted.InvitedByDisplay != _invitedByDisplay ||
             DateOnly.FromDateTime(accepted.AcceptedAt.UtcDateTime) > _guestExpiresOn))
            throw new InvalidOperationException("The accepted guest invitation does not match its scope and expiry.");
        if (_affiliation != "guest" &&
            (accepted.GuestProgramIds is not null || accepted.GuestExpiresOn is not null))
            throw new InvalidOperationException("Only a guest invitation acceptance may carry guest terms.");

        _accepted = true;
        _acceptedUserId = accepted.UserId;
    }

    RequestError? CheckDeliveryAttempt(Uuid deliveryAttemptId, DateTimeOffset attemptedAt) =>
        !_created
            ? new RequestError(RequestErrorKind.NotFound, "The invitation does not exist.")
            : deliveryAttemptId == Uuid.Empty || attemptedAt == default
                ? new RequestError(RequestErrorKind.Validation,
                    "A delivery attempt requires an identity and timestamp.")
                : _deliveryAttemptId != deliveryAttemptId
                    ? new RequestError(RequestErrorKind.Conflict,
                        "The invitation delivery attempt is stale.")
                    : null;

    static Uuid CreateId(Uuid tenantId, string emailAddress) =>
        Uuid.CreateVersion5(InvitationNamespaceId, $"{tenantId}\n{Normalize(emailAddress)}");

    static string Normalize(string emailAddress) =>
        EmailAddresses.TryNormalize(emailAddress, out var normalized)
            ? normalized
            : throw new ArgumentException("A valid email address is required.", nameof(emailAddress));

    static bool IsTokenHash(string? tokenHash) => tokenHash is { Length: 64 } &&
        tokenHash.All(Uri.IsHexDigit);

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
