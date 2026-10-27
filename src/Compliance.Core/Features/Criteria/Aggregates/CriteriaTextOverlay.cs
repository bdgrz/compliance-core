using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>
///     The licensed text overlay of one criterion of one edition in one tenant: a sequential revision line. Its
///     identity derives from the tenant, the edition and the catalog's canonical criterion identifier only, so
///     different criteria never share a stream and edition text is never touched. Every revision references the
///     complete supplied content as verified immutable content; the revision counts the facts of this line.
/// </summary>
public sealed class CriteriaTextOverlay : Aggregate
{
    /// <summary>Stream area of every criteria text overlay (the realm is the tenant).</summary>
    public const string Area = "criteria-text-overlays";

    /// <summary>Schema of the supplied content a revision references.</summary>
    public const string ContentSchema = "CriteriaTextOverlayEntryRevised.OverlayContent.v1";

    /// <summary>Schema of the registration a revision returns.</summary>
    public const string RegistrationSchema = "CriteriaTextOverlay.Registration.v1";

    readonly Uuid _tenantId;
    readonly Uuid _editionId;
    readonly string _identifier;
    CriteriaTextOverlayEntryRevised? _latest;

    /// <summary>Opens the overlay line of one criterion, selecting the logical operation to resolve.</summary>
    public CriteriaTextOverlay(Uuid tenantId, Uuid editionId, string identifier, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, editionId, identifier),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, editionId, identifier).ToString()))
    {
        (_tenantId, _editionId, _identifier) = (tenantId, editionId, identifier);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<CriteriaTextOverlayEntryRevised>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The revision of the latest fact; 0 before the first set.</summary>
    public long Revision => _latest?.Revision ?? 0;

    /// <summary>The latest revision fact, once set.</summary>
    public CriteriaTextOverlayEntryRevised? Latest => _latest;

    /// <summary>The identity of the overlay of one criterion.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid editionId, string identifier) =>
        AggregateIdentity.Create("CriteriaTextOverlay", tenantId, editionId, identifier);

    /// <summary>Checks the revision guard without raising anything.</summary>
    public Result PrepareSet(long expectedRevision) => expectedRevision == Revision
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            $"Reload the overlay before revising. Current revision: {Revision}."));

    /// <summary>Appends the next revision as the root native fact of its operation.</summary>
    public Result Set(OperationProvenance operation, ImmutableContentRef overlayContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(overlayContent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An overlay revision is a root operation of its own overlay with an expected revision."));
        var prepared = PrepareSet((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new CriteriaTextOverlayEntryRevised(_tenantId, _editionId, _identifier, Revision + 1,
            overlayContent, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(CriteriaTextOverlayEntryRevised ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.EditionId != _editionId ||
            !string.Equals(ev.Identifier, _identifier, StringComparison.Ordinal) || ev.Revision != Revision + 1 ||
            ev.FirstResponse is null || ev.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(SetCriteriaTextOverlay), (ulong)Revision))
            throw new InvalidOperationException(
                "An overlay revision must follow its owning tenant, edition, criterion and predecessor.");
        _latest = ev;
    }
}
