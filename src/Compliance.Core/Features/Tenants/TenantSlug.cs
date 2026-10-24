using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The global owner of one normalized slug: at most one tenant claims it and, once surrendered, it is retired for
///     good. Every claim and surrender is a native operation keyed by its logical command id, so a replayed receiver
///     never appends a second answer and an answer is never a bare receipt.
/// </summary>
public sealed class TenantSlug : Aggregate
{
    static Uuid SlugNamespaceId { get; } =
        Uuid.Parse("4f46ea79-15fd-5e46-b715-3e32e5899af8", CultureInfo.InvariantCulture);

    readonly string _slug;
    Uuid? _tenantId;
    bool _retired;

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The tenant already holds the claim, so a further claim by it needs no fact.</summary>
    public static RequestError AlreadyClaimed { get; } = new(RequestErrorKind.Conflict,
        "The tenant already holds this slug.", false);

    /// <summary>The tenant already gave this slug up, so a further surrender by it needs no fact.</summary>
    public static RequestError AlreadySurrendered { get; } = new(RequestErrorKind.Conflict,
        "The tenant already surrendered this slug.", false);

    public Uuid? OwningTenantId => _tenantId;
    public bool IsRetired => _retired;

    public TenantSlug(string slug, Uuid selectedCommandId = default) : this(slug, allowReserved: false, selectedCommandId)
    {
    }

    internal TenantSlug(string slug, bool allowReserved, Uuid selectedCommandId = default)
        : base(CreateId(slug, allowReserved),
            new EventStreamAddress("bdgrz", "tenant-slugs", CreateId(slug, allowReserved).ToString()))
    {
        _slug = Normalize(slug, allowReserved);
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<TenantSlugRegistered>(registered =>
        {
            Accept(registered, registered.CommandId, registered.Operation);
            _tenantId = registered.TenantId;
        });
        On<TenantSlugRegistrationRejected>(rejected => Accept(rejected, rejected.CommandId, rejected.Operation));
        // A slug is permanently spent once surrendered, per the backlog's "a retired slug is never
        // assigned to another organization" rule. Keep the old owner for member-only redirects.
        On<TenantSlugSurrendered>(surrendered =>
        {
            Accept(surrendered, surrendered.CommandId, surrendered.Operation);
            _retired = true;
        });
        On<TenantSlugSurrenderRejected>(rejected => Accept(rejected, rejected.CommandId, rejected.Operation));
    }

    /// <summary>
    ///     Reports whether <paramref name="tenantId" /> could claim this slug right now. A cheap,
    ///     possibly-stale preflight for <see cref="RegisterTenantSlugAvailabilityGuard" />; only
    ///     <see cref="Register" /> decides ownership authoritatively. A retired slug (once
    ///     surrendered) is never available again, even to the tenant that surrendered it.
    /// </summary>
    public bool IsAvailableFor(Uuid tenantId) => !_retired && (_tenantId is null || _tenantId == tenantId);

    /// <summary>
    ///     Checks a claim against hydrated state without raising anything. A tenant that already holds the claim
    ///     reports <see cref="AlreadyClaimed" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareRegister(Uuid tenantId)
    {
        if (tenantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A claim requires a tenant."));
        return !TenantSlugs.IsReservedRoute(_slug) && !_retired && _tenantId == tenantId
            ? Result.Failure(AlreadyClaimed)
            : Result.Success;
    }

    /// <summary>Accepts the claim, or its refusal, as the native fact of <paramref name="operation" />.</summary>
    public Result Register(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid tenantId)
    {
        var prepared = Prepare(operation, firstResponse, PrepareRegister(tenantId));
        if (!prepared.IsSuccess)
            return prepared;
        if (TenantSlugs.IsReservedRoute(_slug) || _retired || _tenantId is not null && _tenantId != tenantId)
            RaiseEvent(new TenantSlugRegistrationRejected(tenantId, _slug, operation.LogicalId, operation,
                firstResponse));
        else
            RaiseEvent(new TenantSlugRegistered(tenantId, _slug, operation.LogicalId, operation, firstResponse));
        return Result.Success;
    }

    /// <summary>
    ///     Checks a surrender against hydrated state without raising anything. A tenant that already gave the slug up
    ///     reports <see cref="AlreadySurrendered" />, which dispatchers treat as a terminal no-op.
    /// </summary>
    public Result PrepareSurrender(Uuid tenantId)
    {
        if (tenantId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A surrender requires a tenant."));
        return _tenantId == tenantId && _retired ? Result.Failure(AlreadySurrendered) : Result.Success;
    }

    /// <summary>Accepts the surrender, or its refusal, as the native fact of <paramref name="operation" />.</summary>
    public Result Surrender(OperationProvenance operation, ImmutableContentRef firstResponse, Uuid tenantId)
    {
        var prepared = Prepare(operation, firstResponse, PrepareSurrender(tenantId));
        if (!prepared.IsSuccess)
            return prepared;
        if (_tenantId == tenantId)
            RaiseEvent(new TenantSlugSurrendered(tenantId, _slug, operation.LogicalId, operation, firstResponse));
        else
            RaiseEvent(new TenantSlugSurrenderRejected(tenantId, _slug, operation.LogicalId, operation,
                firstResponse));
        return Result.Success;
    }

    /// <summary>The provenance of a root operation on this owner.</summary>
    public OperationProvenance Provenance(Uuid commandId, string commandKind, ActorReference actor, string episode,
        string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        OwnerProvenance.For(this, commandId, commandKind, actor, episode, null, authoredIntentSha256, acceptedAt);

    Result Prepare(OperationProvenance operation, ImmutableContentRef firstResponse, Result prepared)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(firstResponse);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A slug operation requires its own logical operation."));
        return prepared;
    }

    void Accept(DomainEvent fact, Uuid commandId, OperationProvenance operation)
    {
        OwnerProvenance.RequireOwn(this, commandId, operation);
        Acceptance.Observe(fact);
    }

    static Uuid CreateId(string slug, bool allowReserved) =>
        Uuid.CreateVersion5(SlugNamespaceId, Normalize(slug, allowReserved));

    static string Normalize(string slug, bool allowReserved)
    {
        var valid = allowReserved
            ? TenantSlugs.TryNormalizeForLookup(slug, out var normalized)
            : TenantSlugs.TryNormalize(slug, out normalized);
        return valid ? normalized : throw new ArgumentException("A valid tenant slug is required.", nameof(slug));
    }
}
