using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests;

/// <summary>Builds well-formed native tenant and slug facts and drives the owners with fresh logical operations.</summary>
static class TenantFacts
{
    static readonly ActorReference Actor = ActorReference.ForSystemProcess("system", "system");
    static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A frozen response reference of the right schema; nothing reads it back.</summary>
    public static ImmutableContentRef Response() => new(Uuid.CreateVersion4(), new string('b', 64), 2,
        "application/json", "first-response-descriptor.v1");

    /// <summary>The provenance of an operation on <paramref name="owner" />.</summary>
    public static OperationProvenance Provenance(Aggregate owner, string commandKind, Uuid commandId = default,
        ActorReference? actor = null, ulong? expectedRevision = null) =>
        OwnerProvenance.For(owner, commandId == Uuid.Empty ? Uuid.CreateVersion4() : commandId, commandKind,
            actor ?? Actor, "current", expectedRevision, new string('a', 64), At);

    public static TenantSlugConfirmed Confirmed(Uuid tenantId, string slug) => Fact(new Tenant(tenantId),
        "ConfirmTenantSlug", (operation, response) => new TenantSlugConfirmed(tenantId, slug, operation.LogicalId,
            operation, response));

    public static TenantSlugRejected Rejected(Uuid tenantId, string slug) => Fact(new Tenant(tenantId),
        "RejectTenantSlug", (operation, response) => new TenantSlugRejected(tenantId, slug, operation.LogicalId,
            operation, response));

    public static TenantSlugRegistered Registered(Uuid tenantId, string slug) =>
        Fact(new TenantSlug(slug, allowReserved: true), "RegisterTenantSlug", (operation, response) =>
            new TenantSlugRegistered(tenantId, slug, operation.LogicalId, operation, response));

    static T Fact<T>(Aggregate owner, string commandKind, Func<OperationProvenance, ImmutableContentRef, T> create) =>
        create(Provenance(owner, commandKind), Response());

    /// <summary>Registers the tenant and reports only whether it succeeded.</summary>
    public static Result Open(this Tenant tenant, Uuid ownerUserId, string name, string slug)
    {
        var registered = tenant.Register(ownerUserId, name, slug);
        return registered.IsSuccess ? Result.Success : Result.Failure(registered.Error!);
    }

    public static Result ConfirmSlug(this Tenant tenant, string slug, Uuid commandId = default) =>
        tenant.ConfirmSlug(Provenance(tenant, "ConfirmTenantSlug", commandId), Response(), slug);

    public static Result RejectSlug(this Tenant tenant, string slug, Uuid commandId = default) =>
        tenant.RejectSlug(Provenance(tenant, "RejectTenantSlug", commandId), Response(), slug);

    public static Result RequestSlugChange(this Tenant tenant, string slug, ActorReference? requestedBy = null,
        DateTimeOffset? requestedAt = null, long expectedRevision = -1, Uuid commandId = default) =>
        tenant.RequestSlugChange(Provenance(tenant, "ChangeTenantSlug", commandId), Response(), slug,
            expectedRevision, requestedBy, requestedAt);

    public static Result RequestSlugSurrender(this Tenant tenant, string slug, Uuid commandId = default) =>
        tenant.RequestSlugSurrender(Provenance(tenant, "RequestTenantSlugSurrender", commandId), Response(), slug);

    public static Result ConfirmSlugSurrender(this Tenant tenant, string slug, Uuid commandId = default) =>
        tenant.ConfirmSlugSurrender(Provenance(tenant, "ConfirmTenantSlugSurrender", commandId), Response(), slug);

    public static Result RejectSlugSurrender(this Tenant tenant, string slug, Uuid commandId = default) =>
        tenant.RejectSlugSurrender(Provenance(tenant, "RejectTenantSlugSurrender", commandId), Response(), slug);

    public static Result Register(this TenantSlug slug, Uuid tenantId, Uuid commandId = default) =>
        slug.Register(Provenance(slug, "RegisterTenantSlug", commandId), Response(), tenantId);

    public static Result Surrender(this TenantSlug slug, Uuid tenantId, Uuid commandId = default) =>
        slug.Surrender(Provenance(slug, "SurrenderTenantSlug", commandId), Response(), tenantId);
}
