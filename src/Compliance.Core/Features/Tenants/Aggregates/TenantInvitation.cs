using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.Tenants.Aggregates;

/// <summary>
///     One immutable invitation offer, identified by its client-chosen invitation id: the exact recipient, purpose,
///     scopes and expiry, one verified token and at most one accepting user. Creation and acceptance are native
///     operations of this owner; delivery attempts and the mailbox claim live in their own owners.
/// </summary>
public sealed class TenantInvitation : Aggregate
{
    /// <summary>Lifetime of an invitation token.</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromDays(7);

    readonly Uuid _tenantId;
    readonly Uuid _invitationId;
    string? _emailAddress;
    string? _tokenHash;
    DateTimeOffset _expiresAt;
    string? _affiliation;
    Uuid _invitedBy;
    string? _invitedByDisplay;
    DateTimeOffset _invitedAt;
    bool _administrator;
    string? _builtInRole;
    ImmutableContentRef? _guestProgramIdsManifest;
    DateOnly? _guestExpiresOn;
    long _mailboxGeneration;
    bool _created;
    bool _accepted;
    Uuid _acceptedUserId;

    /// <summary>Opens the invitation owner; <paramref name="selectedLogicalId" /> selects the operation to resolve for a retry.</summary>
    public TenantInvitation(Uuid tenantId, Uuid invitationId, Uuid selectedLogicalId = default)
        : base(invitationId, new EventStreamAddress(tenantId.ToString(), "tenant-invitations", invitationId.ToString()))
    {
        _tenantId = tenantId;
        _invitationId = invitationId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TenantMemberInvited>(Apply);
        On<TenantInvitationAccepted>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    public bool IsCreated => _created;
    public bool IsAccepted => _accepted;
    public Uuid AcceptedUserId => _acceptedUserId;
    public string? EmailAddress => _emailAddress;
    public string? Affiliation => _affiliation;
    public bool Administrator => _administrator;
    public string? BuiltInRole => _builtInRole;
    public DateTimeOffset ExpiresAt => _expiresAt;
    public DateOnly? GuestExpiresOn => _guestExpiresOn;
    public ImmutableContentRef? GuestProgramIdsManifest => _guestProgramIdsManifest;
    public long MailboxGeneration => _mailboxGeneration;

    /// <summary>True once the invitation can no longer be accepted by time: its token or guest membership ended.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => _created && (now >= _expiresAt ||
        _guestExpiresOn is { } endDate && DateOnly.FromDateTime(now.UtcDateTime) > endDate);

    /// <summary>
    ///     Checks the terms of a new invitation against the offer rules without raising anything; the first half of
    ///     the native acceptance pair.
    /// </summary>
    public Result PrepareInvite(TenantInvitationTerms terms, string tokenHash, DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(terms);
        if (!EmailAddresses.TryNormalize(terms.EmailAddress, out var normalized) || normalized != terms.EmailAddress)
            return Failure(RequestErrorKind.Validation, "Enter a valid email address.");
        var isGuest = terms.Affiliation == "guest";
        if (terms.Affiliation is not ("client_personnel" or "firm_staff" or "guest"))
            return Failure(RequestErrorKind.Validation,
                "Affiliation must be client_personnel, firm_staff, or guest.");
        if (isGuest && (terms.Administrator || terms.BuiltInRole is not null ||
                        terms.GuestProgramIds is not { Count: > 0 } programs ||
                        programs.Any(id => id == Uuid.Empty) ||
                        programs.Distinct().Count() != programs.Count ||
                        terms.GuestExpiresOn is not { } endDate ||
                        endDate <= DateOnly.FromDateTime(now.UtcDateTime) || endDate == DateOnly.MaxValue ||
                        string.IsNullOrWhiteSpace(terms.InvitedByDisplay)))
            return Failure(RequestErrorKind.Validation,
                "A guest invitation requires scoped programs, a future membership end date, and no administrator or built-in role.");
        if (!isGuest && (terms.GuestProgramIds is not null || terms.GuestExpiresOn is not null))
            return Failure(RequestErrorKind.Validation,
                "Only a guest invitation may include guest program scopes or an end date.");
        if (terms.Administrator && terms.Affiliation != "client_personnel")
            return Failure(RequestErrorKind.Validation, "The first administrator must be client personnel.");
        if (terms.Administrator && terms.BuiltInRole is not null)
            return Failure(RequestErrorKind.Validation,
                "A first-administrator invitation cannot select a second role.");
        if (terms.BuiltInRole is not null &&
            (terms.Affiliation != "client_personnel" || BuiltInRbac.TeamIdForRole(_tenantId, terms.BuiltInRole) is null))
            return Failure(RequestErrorKind.Validation,
                "A built-in role requires client personnel and a supported role value.");
        if (_created)
            return Failure(RequestErrorKind.Conflict, "The invitation already exists with different terms.");
        if (!IsTokenHash(tokenHash) || expiresAt != now + Lifetime || terms.InvitedBy == Uuid.Empty || now == default)
            return Failure(RequestErrorKind.Validation, "A valid invitation is required.");
        return Result.Success;
    }

    /// <summary>
    ///     Accepts the creation as the native fact of <paramref name="operation" />. The logical id of the operation
    ///     is the invitation id; <paramref name="mailboxGeneration" /> is the generation of the claim it was
    ///     published under.
    /// </summary>
    public Result Invite(OperationProvenance operation, ImmutableContentRef firstResponse, TenantInvitationTerms terms,
        string tokenHash, string? tokenKeyId, DateTimeOffset expiresAt, DateTimeOffset now, long mailboxGeneration,
        ImmutableContentRef? guestProgramIdsManifest)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId != _invitationId || operation.Owner != Stream)
            return Failure(RequestErrorKind.Validation, "An invitation requires its own logical operation.");
        var prepared = PrepareInvite(terms, tokenHash, expiresAt, now);
        if (!prepared.IsSuccess)
            return prepared;
        if (mailboxGeneration < 1 || (terms.Affiliation == "guest") != (guestProgramIdsManifest is not null))
            return Failure(RequestErrorKind.Validation, "An invitation requires its mailbox claim and exact scopes.");
        RaiseEvent(new TenantMemberInvited(_tenantId, _invitationId, terms.EmailAddress, terms.Affiliation,
            terms.Administrator, tokenHash, expiresAt, terms.InvitedBy, terms.BuiltInRole, tokenKeyId,
            guestProgramIdsManifest, terms.GuestExpiresOn, now, terms.InvitedByDisplay?.Trim(), mailboxGeneration,
            operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks an acceptance against the pending offer without raising anything. An exact retry of an accepted
    ///     operation never reaches this check, so expiry cannot turn a replay into a failure.
    /// </summary>
    public Result PrepareAccept(Uuid userId, string emailAddress, string token, DateTimeOffset now)
    {
        if (!_created || _tokenHash is null)
            return Failure(RequestErrorKind.Validation, "A valid invitation token is required.");
        if (_accepted)
            return Failure(RequestErrorKind.Forbidden, "The invitation belongs to another user.");
        if (userId == Uuid.Empty || string.IsNullOrWhiteSpace(token))
            return Failure(RequestErrorKind.Validation, "A valid invitation token is required.");
        if (!EmailAddresses.TryNormalize(emailAddress, out var normalized) || normalized != _emailAddress)
            return Failure(RequestErrorKind.Validation, "The invitation token is invalid.");
        if (now >= _expiresAt)
            return Failure(RequestErrorKind.Validation, "The invitation has expired.");
        if (_invitedAt != default && now < _invitedAt)
            return Failure(RequestErrorKind.Validation, "An invitation cannot be accepted before it was issued.");
        if (_affiliation == "guest" && _guestExpiresOn is { } guestExpiresOn &&
            DateOnly.FromDateTime(now.UtcDateTime) > guestExpiresOn)
            return Failure(RequestErrorKind.Validation, "The guest membership has expired.");

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expectedHash = Convert.FromHexString(_tokenHash);
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash)
            ? Result.Success
            : Failure(RequestErrorKind.Validation, "The invitation token is invalid.");
    }

    /// <summary>
    ///     Accepts the acceptance as the native fact of <paramref name="operation" />. The accepted guest scope
    ///     manifest must carry exactly the offered scope content.
    /// </summary>
    public Result Accept(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid userId,
        string emailAddress, string token, DateTimeOffset now, ImmutableContentRef? acceptedGuestProgramIdsManifest)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId != TenantInvitationIds.AcceptanceOf(_invitationId) || operation.Owner != Stream)
            return Failure(RequestErrorKind.Validation, "An acceptance requires its own logical operation.");
        var prepared = PrepareAccept(userId, emailAddress, token, now);
        if (!prepared.IsSuccess)
            return prepared;
        if (!SameScope(_guestProgramIdsManifest, acceptedGuestProgramIdsManifest))
            return Failure(RequestErrorKind.Conflict, "The accepted guest scope differs from the offer.");
        RaiseEvent(new TenantInvitationAccepted(_tenantId, _invitationId, userId, _emailAddress!, _affiliation!,
            _administrator, _builtInRole, acceptedGuestProgramIdsManifest, _guestExpiresOn, now, _invitedBy,
            _invitedByDisplay, operation, firstResponse));
        return Result.Success;
    }

    void Apply(TenantMemberInvited invited)
    {
        Acceptance.Observe(invited);
        if (_created || invited.TenantId != _tenantId || invited.InvitationId != _invitationId ||
            invited.Operation.LogicalId != _invitationId || invited.Operation.Owner != Stream ||
            !EmailAddresses.TryNormalize(invited.EmailAddress, out var normalized) || normalized != invited.EmailAddress)
            throw new InvalidOperationException("The invitation does not match its tenant, id and email address.");
        if (invited.Affiliation is not ("client_personnel" or "firm_staff" or "guest") ||
            !IsTokenHash(invited.TokenHash) || invited.ExpiresAt == default ||
            invited.InvitedBy == Uuid.Empty || invited.MailboxGeneration < 1 ||
            invited.InvitedAt != default && invited.ExpiresAt <= invited.InvitedAt ||
            invited.Administrator && invited.Affiliation != "client_personnel" ||
            invited.BuiltInRole is not null &&
            (invited.Affiliation != "client_personnel" ||
             BuiltInRbac.TeamIdForRole(_tenantId, invited.BuiltInRole) is null))
            throw new InvalidOperationException("The invitation event has invalid terms.");
        var isGuest = invited.Affiliation == "guest";
        if (isGuest && (invited.Administrator || invited.BuiltInRole is not null ||
                        invited.InvitedAt == default || invited.GuestProgramIdsManifest is null ||
                        invited.GuestExpiresOn is not { } expiresOn ||
                        expiresOn <= DateOnly.FromDateTime(invited.InvitedAt.UtcDateTime) ||
                        expiresOn == DateOnly.MaxValue ||
                        string.IsNullOrWhiteSpace(invited.InvitedByDisplay)))
            throw new InvalidOperationException("The guest invitation event has invalid scope or expiry terms.");
        if (!isGuest && (invited.GuestProgramIdsManifest is not null || invited.GuestExpiresOn is not null))
            throw new InvalidOperationException("Only a guest invitation event may carry guest scope terms.");
        _created = true;
        _emailAddress = invited.EmailAddress;
        _tokenHash = invited.TokenHash;
        _expiresAt = invited.ExpiresAt;
        _affiliation = invited.Affiliation;
        _invitedBy = invited.InvitedBy;
        _invitedByDisplay = invited.InvitedByDisplay?.Trim();
        _invitedAt = invited.InvitedAt;
        _administrator = invited.Administrator;
        _builtInRole = invited.BuiltInRole;
        _guestProgramIdsManifest = invited.GuestProgramIdsManifest;
        _guestExpiresOn = invited.GuestExpiresOn;
        _mailboxGeneration = invited.MailboxGeneration;
    }

    void Apply(TenantInvitationAccepted accepted)
    {
        Acceptance.Observe(accepted);
        if (!_created || _accepted || accepted.TenantId != _tenantId || accepted.InvitationId != _invitationId ||
            accepted.Operation.LogicalId != TenantInvitationIds.AcceptanceOf(_invitationId) ||
            accepted.Operation.Owner != Stream || accepted.EmailAddress != _emailAddress ||
            accepted.UserId == Uuid.Empty || accepted.Affiliation != _affiliation ||
            accepted.Administrator != _administrator || accepted.BuiltInRole != _builtInRole)
            throw new InvalidOperationException("The accepted invitation does not match its pending terms.");
        if (accepted.AcceptedAt != default && accepted.AcceptedAt >= _expiresAt)
            throw new InvalidOperationException("An expired invitation cannot be accepted.");
        if (accepted.AcceptedAt != default && _invitedAt != default && accepted.AcceptedAt < _invitedAt)
            throw new InvalidOperationException("An invitation cannot be accepted before it was issued.");
        if (_affiliation == "guest" &&
            (accepted.AcceptedAt == default || accepted.GuestExpiresOn != _guestExpiresOn ||
             accepted.AcceptedAt < _invitedAt || !SameScope(_guestProgramIdsManifest, accepted.GuestProgramIdsManifest) ||
             accepted.InvitedBy != _invitedBy || accepted.InvitedByDisplay != _invitedByDisplay ||
             DateOnly.FromDateTime(accepted.AcceptedAt.UtcDateTime) > _guestExpiresOn))
            throw new InvalidOperationException("The accepted guest invitation does not match its scope and expiry.");
        if (_affiliation != "guest" && (accepted.GuestProgramIdsManifest is not null || accepted.GuestExpiresOn is not null))
            throw new InvalidOperationException("Only a guest invitation acceptance may carry guest terms.");

        _accepted = true;
        _acceptedUserId = accepted.UserId;
    }

    // The accepted manifest is separate content under its own schema; equal scope means equal bytes and digest.
    static bool SameScope(ImmutableContentRef? offered, ImmutableContentRef? accepted) =>
        offered is null ? accepted is null
            : accepted is not null && offered.Sha256 == accepted.Sha256 && offered.Bytes == accepted.Bytes;

    static bool IsTokenHash(string? tokenHash) => tokenHash is { Length: 64 } && tokenHash.All(Uri.IsHexDigit);

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
