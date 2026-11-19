using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>One immutable metadata fact for one platform criteria seed edition.</summary>
/// <remarks>
/// A trusted seed participant must verify the exact committed edition request and
/// its version and fingerprint before calling <see cref="Record" />. A source
/// event ID or actor supplied by an untrusted caller is not source authority.
/// </remarks>
public sealed class CriteriaSeedMetadata : Aggregate
{
    readonly Uuid _editionId;
    CriteriaSeedMetadataRecorded? _recorded;

    /// <summary>Opens the initial metadata owner for one edition.</summary>
    public CriteriaSeedMetadata(Uuid editionId)
        : base(IdentityOf(editionId), AddressOf(editionId))
    {
        _editionId = editionId;
        On<CriteriaSeedMetadataRecorded>(Apply);
    }

    /// <summary>Whether the initial metadata fact has been raised or replayed.</summary>
    public bool IsRecorded => _recorded is not null;

    /// <summary>The exact initial metadata fact, if recorded.</summary>
    public CriteriaSeedMetadataRecorded? Recorded => _recorded;

    /// <summary>Returns one edition's deterministic metadata identity.</summary>
    public static Uuid IdentityOf(Uuid editionId)
    {
        EnsureIdentity(editionId);
        return AggregateIdentity.Create(nameof(CriteriaSeedMetadata), editionId);
    }

    /// <summary>Returns the metadata stream in the edition's ordered source area.</summary>
    public static EventStreamAddress AddressOf(Uuid editionId)
    {
        EnsureIdentity(editionId);
        return new EventStreamAddress("bdgrz", $"criteria-{editionId}", $"seed-metadata-{editionId}");
    }

    /// <summary>Records the initial source-verified metadata or returns its exact prior outcome.</summary>
    public Result Record(Uuid operationId, string seedVersion, string seedFingerprint,
        Uuid editionRequestEventId, string framework, DateTimeOffset publishedAt, string coverageNote,
        string sourceUrl, string contentRights, bool numberedCriteriaComplete, ActorAttribution recordedBy)
    {
        if (!CriteriaContentBounds.Text(seedVersion, CriteriaContentBounds.SeedVersionBytes) ||
            !CriteriaContentBounds.Text(coverageNote, CriteriaContentBounds.CoverageNoteBytes) ||
            !CriteriaContentBounds.Text(sourceUrl, CriteriaContentBounds.SourceUrlBytes) ||
            !CriteriaContentBounds.ActorFields(recordedBy))
            return Failure(RequestErrorKind.Validation,
                "Seed metadata fields must fit the criteria UTF-8 and 8192-byte JSON payload limits.");
        if (!Valid(operationId, seedVersion, seedFingerprint, editionRequestEventId, framework, publishedAt,
                coverageNote, sourceUrl, contentRights, recordedBy))
            return Failure(RequestErrorKind.Validation,
                "Seed metadata needs an operation, exact binding and request, source metadata, and actor.");

        var fact = new CriteriaSeedMetadataRecorded(_editionId, operationId, seedVersion, seedFingerprint,
            editionRequestEventId, framework, publishedAt, coverageNote, sourceUrl, contentRights,
            numberedCriteriaComplete, recordedBy);
        if (_recorded is not null)
            return _recorded == fact
                ? Result.Success
                : Failure(RequestErrorKind.Conflict, "The edition metadata identity already has a different seed intent.");

        if (!CriteriaContentBounds.Payload(fact))
            return Failure(RequestErrorKind.Validation, "A criteria event's JSON business payload must fit within 8192 bytes.");
        RaiseEvent(fact);
        return Result.Success;
    }

    void Apply(CriteriaSeedMetadataRecorded fact)
    {
        if (_recorded is not null || fact.EditionId != _editionId ||
            !Valid(fact.OperationId, fact.SeedVersion, fact.SeedFingerprint, fact.EditionRequestEventId,
                fact.Framework, fact.PublishedAt, fact.CoverageNote, fact.SourceUrl, fact.ContentRights,
                fact.RecordedBy) || !CriteriaContentBounds.Payload(fact))
            throw new InvalidOperationException("Seed metadata must be one valid fact for its exact edition identity.");

        _recorded = fact;
    }

    static bool Valid(Uuid operationId, string? seedVersion, string? fingerprint, Uuid editionRequestEventId,
        string? framework, DateTimeOffset publishedAt, string? coverageNote, string? sourceUrl,
        string? contentRights, ActorAttribution? actor) =>
        operationId != Uuid.Empty && editionRequestEventId != Uuid.Empty &&
        CriteriaContentBounds.Text(seedVersion, CriteriaContentBounds.SeedVersionBytes) && IsFingerprint(fingerprint) && framework == "tsc" &&
        publishedAt != default && publishedAt.Offset == TimeSpan.Zero &&
        CriteriaContentBounds.Text(coverageNote, CriteriaContentBounds.CoverageNoteBytes) &&
        CriteriaContentBounds.Text(sourceUrl, CriteriaContentBounds.SourceUrlBytes) &&
        contentRights == "identifiers_and_original_summaries" && CriteriaContentBounds.ActorFields(actor) && actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Subject) && !string.IsNullOrWhiteSpace(actor.Issuer);

    static bool IsFingerprint(string? value) => value is { Length: 64 } &&
        value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static void EnsureIdentity(Uuid editionId)
    {
        if (editionId == Uuid.Empty)
            throw new ArgumentException("Seed metadata requires a non-empty edition identity.", nameof(editionId));
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
