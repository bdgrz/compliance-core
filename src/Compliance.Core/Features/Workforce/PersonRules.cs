using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Complete detached person values and pure rules; never native command authority.</summary>
public sealed class PersonRules
{
    readonly Uuid _tenantId;
    readonly List<DomainEvent> _pending = [];
    bool _created;
    long _revision;
    string? _initialDisplayName;
    string? _initialWorkEmail;
    PersonalContactDetails? _initialPersonalContact;
    PersonalContactDetails? _personalContact;
    Uuid? _correlatedUserId;
    public PersonRules(Uuid tenantId, Uuid personId, PersonView? current = null)
    {
        _tenantId = tenantId;
        Id = personId;
        Current = current;
        _created = current is not null;
        _revision = current?.Revision ?? 0;
        _correlatedUserId = current?.CorrelatedUserId;
        _personalContact = current?.PersonalContact;
        _initialDisplayName = current?.DisplayName;
        _initialWorkEmail = current?.WorkEmail;
        _initialPersonalContact = current?.PersonalContact;
    }
    public Uuid TenantId => _tenantId;
    public Uuid Id { get; }
    public EventStreamAddress Stream => new(_tenantId.ToString(), "people", Id.ToString());
    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid? CorrelatedUserId => _correlatedUserId;
    public PersonView? Current { get; private set; }
    public IReadOnlyList<DomainEvent> PendingEvents => _pending;
    void RaiseEvent(DomainEvent value)
    {
        Observe(value);
        _pending.Add(value);
    }
    public void Observe(DomainEvent value)
    {
        switch (value)
        {
            case PersonRecorded ev:
                _created = true;
                _revision = 1;
                _initialDisplayName = ev.DisplayName;
                _initialWorkEmail = ev.WorkEmail;
                _initialPersonalContact = Normalize(ev.PersonalContact);
                _personalContact = _initialPersonalContact;
                Current = new PersonView(_tenantId, Id, 1, ev.DisplayName, ev.WorkEmail, "manual", ev.Actor, ev.ChangedAt,
                    PersonalContact: _personalContact);
                break;
            case PersonRevised ev:
                if (Current is null) throw new InvalidOperationException("The person revision has no creation.");
                _revision = ev.Revision;
                _personalContact = ev.PersonalContact;
                Current = Current with { Revision = ev.Revision, DisplayName = ev.DisplayName, WorkEmail = ev.WorkEmail,
                    PersonalContact = ev.PersonalContact, LastChangedBy = ev.Actor, LastChangedAt = ev.ChangedAt };
                break;
            case PersonMembershipCorrelated ev:
                if (Current is null) throw new InvalidOperationException("The person correlation has no creation.");
                _revision = ev.Revision;
                _correlatedUserId = ev.UserId;
                Current = Current with { Revision = ev.Revision, CorrelatedUserId = ev.UserId,
                    LastChangedBy = ev.Actor, LastChangedAt = ev.ChangedAt };
                break;
            default: throw new InvalidOperationException("Another person values mutation kind.");
        }
    }
    public Result<PersonRegistration> Record(string displayName, string? workEmail,
        ActorReference actor, DateTimeOffset changedAt)
        => Record(displayName, workEmail, null, actor, changedAt);

    public Result<PersonRegistration> Record(string displayName, string? workEmail,
        PersonalContactDetails? personalContact, ActorReference actor, DateTimeOffset changedAt)
    {
        var validation = Validate(displayName, workEmail);
        if (validation is not null)
            return Result<PersonRegistration>.Failure(validation);
        var name = displayName.Trim();
        var email = NormalizeOptional(workEmail);
        var contact = Normalize(personalContact);
        if (_created)
            return _initialDisplayName == name && _initialWorkEmail == email &&
                   _initialPersonalContact == contact
                ? Result<PersonRegistration>.Success(new PersonRegistration(Id))
                : Result<PersonRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The person already exists with different content."));
        RaiseEvent(new PersonRecorded(_tenantId, Id, name, email, actor, changedAt,
            contact));
        return Result<PersonRegistration>.Success(new PersonRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, string displayName, string? workEmail,
        ActorReference actor, DateTimeOffset changedAt)
        => Revise(expectedRevision, displayName, workEmail, _personalContact, actor, changedAt);

    public CommandFailure? Revise(long expectedRevision, string displayName, string? workEmail,
        PersonalContactDetails? personalContact, ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The person was not found.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("person", _revision));
        var validation = Validate(displayName, workEmail);
        if (validation is not null)
            return CommandFailure.InvalidContent(validation.Message!);
        RaiseEvent(new PersonRevised(_tenantId, Id, _revision + 1, displayName.Trim(),
            NormalizeOptional(workEmail), actor, changedAt, Normalize(personalContact)));
        return null;
    }

    /// <summary>
    ///     Links this person to one platform member, or clears the link. Re-stating the current link
    ///     succeeds without a new revision. The link never grants or revokes platform access.
    /// </summary>
    public CommandFailure? CorrelateMembership(long expectedRevision, Uuid? userId,
        ActorReference actor, DateTimeOffset changedAt)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The person was not found.");
        if (userId == Uuid.Empty)
            return CommandFailure.InvalidContent("A correlated platform member ID cannot be empty.");
        if (expectedRevision != _revision)
            return CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("person", _revision));
        if (userId == _correlatedUserId)
            return null;
        RaiseEvent(new PersonMembershipCorrelated(_tenantId, Id, _revision + 1, userId, actor,
            changedAt));
        return null;
    }

    static RequestError? Validate(string displayName, string? workEmail)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
            return new RequestError(RequestErrorKind.Validation,
                "A person requires a display name of at most 200 characters.");
        var email = NormalizeOptional(workEmail);
        if (email is not null && (email.Length > 320 || email.IndexOf('@') is < 1 ||
                                  email.IndexOf('@') == email.Length - 1 ||
                                  email.Any(char.IsWhiteSpace)))
            return new RequestError(RequestErrorKind.Validation,
                "A work email must be a single address of at most 320 characters.");
        return null;
    }

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static PersonalContactDetails? Normalize(PersonalContactDetails? value)
    {
        if (value is null)
            return null;
        var email = NormalizeOptional(value.PersonalEmail);
        var phone = NormalizeOptional(value.PersonalPhone);
        return email is null && phone is null ? null : new PersonalContactDetails(email, phone);
    }
}
