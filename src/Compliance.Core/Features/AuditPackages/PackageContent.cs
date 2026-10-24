using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Verified immutable package content; candidates use fresh ids until an owner's append accepts one.</summary>
static class PackageContent
{
    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenant, string schema,
        T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), schema, value, ct);

    public static Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenant, ImmutableContentRef reference,
        CancellationToken ct) where T : notnull => content.ReadJsonAsync<T>(tenant.ToString(), reference, ct);

    public static DurableIntentHeader Header(OperationProvenance operation) => new(operation.RootOperationId,
        operation.Owner, operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
        operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt);

    public static async ValueTask<PreparedPackageDelta> DeltaAsync<T>(IImmutableContentStore content, Uuid tenant,
        string schema, T value, CancellationToken ct) where T : notnull =>
        new(await WriteAsync(content, tenant, schema, value, ct).ConfigureAwait(false));

    public static async Task<ImmutableContentRef> FreezeUnitResultAsync(IImmutableContentStore content, Uuid tenant,
        CancellationToken ct)
    {
        var value = await WriteAsync(content, tenant, UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return await WriteAsync(content, tenant, "mutation-result-content.v1",
            new MutationResultContent(UnitResult.Schema, new StandardResponseBody(value)), ct).ConfigureAwait(false);
    }

}
