using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>One immutable disclosed support limit for one platform seed edition.</summary>
/// <remarks>
/// A trusted seed participant must verify the exact committed edition request and
/// its version/fingerprint before calling <see cref="Record" />. An event ID or
/// a system actor supplied by an untrusted caller is not source authority.
/// </remarks>
public sealed class CriteriaSeedDisclosure : Aggregate
{
    readonly Uuid _editionId;
    readonly string _category;
    readonly string _code;
    CriteriaSeedDisclosureRecorded? _recorded;

    /// <summary>Opens one edition/category/code disclosure.</summary>
    public CriteriaSeedDisclosure(Uuid editionId, string category, string code)
        : base(IdentityOf(editionId, category, code), AddressOf(editionId, category, code))
    {
        _editionId = editionId;
        _category = category;
        _code = code;
        On<CriteriaSeedDisclosureRecorded>(Apply);
    }

    /// <summary>Whether the disclosure fact has been raised or replayed.</summary>
    public bool IsRecorded => _recorded is not null;

    /// <summary>The exact disclosure fact, if recorded.</summary>
    public CriteriaSeedDisclosureRecorded? Recorded => _recorded;

    /// <summary>Returns the deterministic identity for one disclosure.</summary>
    public static Uuid IdentityOf(Uuid editionId, string category, string code)
    {
        EnsureIdentity(editionId, category, code);
        return AggregateIdentity.Create(nameof(CriteriaSeedDisclosure), editionId, category, code);
    }

    /// <summary>Returns the disclosure stream in the edition's ordered source area.</summary>
    public static EventStreamAddress AddressOf(Uuid editionId, string category, string code) =>
        new("bdgrz", $"criteria-{editionId}", $"disclosure-{IdentityOf(editionId, category, code)}");

    /// <summary>Records an immutable source-verified disclosure or returns its exact prior result.</summary>
    public Result Record(Uuid operationId, string seedVersion, string seedFingerprint,
        Uuid editionRequestEventId, string note, ActorAttribution recordedBy)
    {
        if (!CriteriaContentBounds.Text(seedVersion, CriteriaContentBounds.SeedVersionBytes) ||
            !CriteriaContentBounds.Text(note, CriteriaContentBounds.DisclosureNoteBytes) ||
            !CriteriaContentBounds.ActorFields(recordedBy))
            return Failure(RequestErrorKind.Validation,
                "Seed disclosure fields must fit the criteria UTF-8 and 8192-byte JSON payload limits.");
        if (!Valid(operationId, seedVersion, seedFingerprint, editionRequestEventId, note, recordedBy))
            return Failure(RequestErrorKind.Validation,
                "A disclosure needs an operation, exact seed binding, edition request, note and actor.");

        if (_recorded is not null)
        {
            return _recorded.OperationId == operationId && _recorded.SeedVersion == seedVersion &&
                   _recorded.SeedFingerprint == seedFingerprint &&
                   _recorded.EditionRequestEventId == editionRequestEventId && _recorded.Note == note &&
                   _recorded.RecordedBy == recordedBy
                ? Result.Success
                : Failure(RequestErrorKind.Conflict, "The disclosure identity already has a different seed intent.");
        }

        var fact = new CriteriaSeedDisclosureRecorded(_editionId, _category, _code, operationId, seedVersion,
            seedFingerprint, editionRequestEventId, note, recordedBy);
        if (!CriteriaContentBounds.Payload(fact))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(fact);
        return Result.Success;
    }

    void Apply(CriteriaSeedDisclosureRecorded fact)
    {
        if (_recorded is not null || fact.EditionId != _editionId ||
            !string.Equals(fact.Category, _category, StringComparison.Ordinal) ||
            !string.Equals(fact.Code, _code, StringComparison.Ordinal) ||
            !Valid(fact.OperationId, fact.SeedVersion, fact.SeedFingerprint, fact.EditionRequestEventId,
                fact.Note, fact.RecordedBy) || !CriteriaContentBounds.Payload(fact))
            throw new InvalidOperationException("A seed disclosure must be one valid fact for its exact identity.");

        _recorded = fact;
    }

    static bool Valid(Uuid operationId, string? seedVersion, string? fingerprint, Uuid editionRequestEventId,
        string? note, ActorAttribution? actor) =>
        operationId != Uuid.Empty && editionRequestEventId != Uuid.Empty &&
        CriteriaContentBounds.Text(seedVersion, CriteriaContentBounds.SeedVersionBytes) && IsFingerprint(fingerprint) &&
        CriteriaContentBounds.Text(note, CriteriaContentBounds.DisclosureNoteBytes) &&
        CriteriaContentBounds.ActorFields(actor) && actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Subject) && !string.IsNullOrWhiteSpace(actor.Issuer);

    static bool IsFingerprint(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static void EnsureIdentity(Uuid editionId, string? category, string? code)
    {
        if (editionId == Uuid.Empty || category is not ("security" or "availability" or "confidentiality" or
                "processing_integrity" or "privacy") || !CriteriaContentBounds.Text(code, CriteriaContentBounds.DisclosureCodeBytes) ||
            !string.Equals(code, code.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A disclosure requires an edition, known category and canonical code.");
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
