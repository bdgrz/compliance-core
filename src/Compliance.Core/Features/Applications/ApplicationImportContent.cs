using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Reads and writes the immutable content referenced by import facts; the realm is the tenant. Every selection is
///     the complete original list, so no list is capped by an event or frame size: the content store chunks it.
/// </summary>
static class ApplicationImportContent
{
    /// <summary>Where a caller reads the status of an accepted operation.</summary>
    public const string StatusUrlTemplate = "/api/v1/platform/operations/{operation_id}";

    /// <summary>Schema of the registration a staging returns.</summary>
    public const string RegistrationSchema = "ApplicationImportStaged.Registration.v1";

    public static string Realm(Uuid tenantId) => tenantId.ToString();

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId, string schema,
        T value, CancellationToken ct = default) where T : notnull =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), schema, value, ct);

    public static Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenantId, ImmutableContentRef reference,
        CancellationToken ct = default) where T : notnull => content.ReadJsonAsync<T>(Realm(tenantId), reference, ct);

    /// <summary>Writes a complete item list as one immutable selection whose count and digest cover every item.</summary>
    public static async Task<ImmutableSelectionManifest> WriteSelectionAsync<T>(IImmutableContentStore content,
        Uuid tenantId, string itemSchema, IReadOnlyList<T> items, SelectionOrder order, CancellationToken ct = default)
    {
        var reference = await WriteAsync(content, tenantId, itemSchema, items, ct).ConfigureAwait(false);
        return new ImmutableSelectionManifest(reference, itemSchema, items.Count, order, reference.Sha256);
    }

    /// <summary>Reads every item of a selection, failing closed on any count or digest mismatch.</summary>
    public static async Task<IReadOnlyList<T>> ReadSelectionAsync<T>(IImmutableContentStore content, Uuid tenantId,
        ImmutableSelectionManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.ItemSchema != manifest.Content.Schema || !Enum.IsDefined(manifest.Order))
            throw new ImmutableContentIntegrityException("The complete import selection has another content schema or ordering contract.");
        var items = await ReadAsync<IReadOnlyList<T>>(content, tenantId, manifest.Content, ct).ConfigureAwait(false);
        if (items.Count != manifest.Count || !string.Equals(manifest.ItemsSha256, manifest.Content.Sha256,
                StringComparison.Ordinal))
            throw new ImmutableContentIntegrityException("The selection does not match its manifest count and digest.");
        return items;
    }

    /// <summary>The digest of a typed content value, for manifests that compare structure rather than reference.</summary>
    public static string Digest<T>(T value) where T : notnull => ContentDigest.OfJson(value);

    /// <summary>The frozen 200 first response whose body is the given content.</summary>
    public static FirstResponseDescriptor OkResponse(string schema, ImmutableContentRef body) => new(200,
        ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(body));

    /// <summary>Writes the mutation result that wraps an already written response body.</summary>
    public static Task<ImmutableContentRef> WriteResultAsync(IImmutableContentStore content, Uuid tenantId,
        string schema, ImmutableContentRef body, CancellationToken ct) =>
        content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(schema, new StandardResponseBody(body)), ct);

    /// <summary>The frozen 200 first response and result of an operation that returns no value.</summary>
    public static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> UnitAsync(
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        var unit = await WriteAsync(content, tenantId, UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        var result = await WriteResultAsync(content, tenantId, UnitResult.Schema, unit, ct).ConfigureAwait(false);
        return (OkResponse(UnitResult.Schema, unit), result);
    }

    /// <summary>The frozen 202 first response of a root admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content,
        Uuid tenantId, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var acknowledgment = await WriteAsync(content, tenantId, "operation-accepted-response.v1",
            new OperationAcceptedResponse(operationId,
                StatusUrlTemplate.Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal),
                acceptedAt), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
            "operation-accepted-response.v1", new StandardResponseBody(acknowledgment));
    }

    /// <summary>The Unit result a terminal fact references as its operation result.</summary>
    public static async Task<ImmutableContentRef> UnitResultAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid rootOperationId, CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion5(rootOperationId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return await content.WriteJsonAsync(Realm(tenantId),
            Uuid.CreateVersion5(rootOperationId, "mutation-result-content.v1"), "mutation-result-content.v1",
            new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)), ct).ConfigureAwait(false);
    }

    /// <summary>The typed value of an accepted or replayed 200 admission, read from its frozen first response.</summary>
    public static async Task<Result<T>> ResponseAsync<T>(IImmutableContentStore content, Uuid tenantId,
        Result<AcceptedAppendResult> outcome, CancellationToken ct) where T : notnull
    {
        if (!outcome.IsSuccess)
            return Result<T>.Failure(outcome.Error!);
        var descriptor = DescriptorOf(outcome.Value!);
        if (descriptor is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("An import response is standard content.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The closed Unit result of an accepted or replayed 200 admission.</summary>
    public static Result UnitOutcome(Result<AcceptedAppendResult> outcome) => !outcome.IsSuccess
        ? Result.Failure(outcome.Error!)
        : DescriptorOf(outcome.Value!) is null ? Result.Failure(AcceptedAppendResult.ConflictError) : Result.Success;

    /// <summary>The 202 acknowledgment of an accepted or replayed admission, read from the frozen first response.</summary>
    public static Task<Result<OperationAcceptedResponse>> AcknowledgmentAsync(IImmutableContentStore content,
        Uuid tenantId, Result<AcceptedAppendResult> outcome, CancellationToken ct) =>
        ResponseAsync<OperationAcceptedResponse>(content, tenantId, outcome, ct);

    static FirstResponseDescriptor? DescriptorOf(AcceptedAppendResult outcome) => outcome switch
    {
        AcceptedAppendResult.Committed committed => committed.Response,
        AcceptedAppendResult.Replayed replayed => replayed.Response,
        _ => null,
    };

    /// <summary>The wire size of a value, to refuse content beyond the store's single-write bound.</summary>
    public static int Size<T>(T value) where T : notnull =>
        JsonSerializer.SerializeToUtf8Bytes(value, ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!).Length;
}
