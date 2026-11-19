using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>Records one completion for the configured fixed seed, without retaining its members.</summary>
/// <remarks>The internal bootstrap must verify each expected source outcome and publication before recording.</remarks>
public sealed class CriteriaSeedImport : Aggregate
{
    readonly Uuid _editionId;
    CriteriaSeedImportCompleted? _completed;

    public CriteriaSeedImport(Uuid editionId) : base(IdentityOf(editionId), AddressOf(editionId))
    {
        _editionId = editionId;
        On<CriteriaSeedImportCompleted>(Apply);
    }

    public static Uuid IdentityOf(Uuid editionId) => editionId == Uuid.Empty
        ? throw new ArgumentException("An edition identity is required.", nameof(editionId))
        : AggregateIdentity.Create(nameof(CriteriaSeedImport), editionId);

    public static EventStreamAddress AddressOf(Uuid editionId) =>
        new("bdgrz", $"criteria-{editionId}", $"seed-import-{IdentityOf(editionId)}");

    public CriteriaSeedImportCompleted? Completed => _completed;

    internal Result Complete(Uuid operationId, Uuid requestEventId, string version, string fingerprint,
        Uuid publicationEventId)
    {
        if (!Valid(operationId, requestEventId, version, fingerprint, publicationEventId))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The exact configured seed and publication are required."));
        var fact = new CriteriaSeedImportCompleted(_editionId, operationId, requestEventId, version, fingerprint,
            publicationEventId, new ActorAttribution("criteria-seed-bootstrap", "system"));
        if (_completed is not null)
            return _completed.EditionId == _editionId && _completed.OperationId == operationId &&
                _completed.EditionRequestEventId == requestEventId && _completed.SeedVersion == version &&
                _completed.SeedFingerprint == fingerprint && _completed.PublicationEventId == publicationEventId &&
                _completed.RecordedBy == fact.RecordedBy ? Result.Success :
                Result.Failure(new RequestError(RequestErrorKind.Conflict, "The seed import was completed with different intent."));
        RaiseEvent(fact);
        return Result.Success;
    }

    void Apply(CriteriaSeedImportCompleted fact)
    {
        if (_completed is not null || fact.EditionId != _editionId ||
            fact.RecordedBy != new ActorAttribution("criteria-seed-bootstrap", "system") ||
            !Valid(fact.OperationId, fact.EditionRequestEventId, fact.SeedVersion, fact.SeedFingerprint, fact.PublicationEventId))
            throw new InvalidOperationException("The seed import completion is inconsistent.");
        _completed = fact;
    }

    bool Valid(Uuid operationId, Uuid requestEventId, string version, string fingerprint, Uuid publicationEventId) =>
        requestEventId != Uuid.Empty && publicationEventId != Uuid.Empty &&
        operationId == PlatformCriteriaSeedDefinition.OperationOf(_editionId, "complete") &&
        PlatformCriteriaSeedDefinition.MatchesBinding(_editionId, version, fingerprint);
}
