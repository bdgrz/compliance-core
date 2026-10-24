using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NamedRecords;

/// <summary>
///     Admits one provider, location or operational process change as the first native fact on its record owner. An
///     exact retry is resolved from the owner's accepted facts before any mutable source is read, a new change then
///     freezes its complete normalized content, claim keys and the fixed 202 acknowledgment before the append, and the
///     name claims, the application and the old-claim release follow through the kind's name-change process.
/// </summary>
public sealed class NamedRecordAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
{
    /// <summary>The HTTP status an accepted change answers with while the process continues.</summary>
    public const int AcceptedStatus = 202;

    /// <summary>Admits the change, or replays the response its logical identity already froze.</summary>
    public async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync<TRecord>(NamedRecordKindDescriptor kind,
        IExecutionContext context, NamedRecordAdmissionRequest request, Func<TRecord, AcceptAttempt, Result> accept,
        CancellationToken ct) where TRecord : NamedRecord
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var realm = request.TenantId.ToString();
        var stream = kind.OpenRecord(request.TenantId, request.RecordId, request.LogicalId).Stream;
        var acceptedAt = clock.GetUtcNow();
        var provenance = new OperationProvenance(request.LogicalId, request.LogicalId, null,
            request.Create ? kind.CreateCommandKind : kind.ReviseCommandKind, request.Actor, stream, Episode, request.Create
                ? null
                : request.ExpectedRevision >= 0 ? (ulong)request.ExpectedRevision : null,
            request.AuthoredIntentSha256, acceptedAt, stream, stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        TRecord Open() => (TRecord)kind.OpenRecord(request.TenantId, request.RecordId, request.LogicalId);

        var hydrated = await reader.HydrateAsync(Open(), ct).ConfigureAwait(false);
        if (hydrated.Acceptance.Decide(binding) != NativeAcceptanceDecision.None)
        {
            var replayed = await operations.ExecuteAsync(new NativeOperation<TRecord>(binding, Open,
                record => record.Acceptance,
                _ => Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The accepted operation could not be replayed.")),
                (_, _) => Result.Failure(AcceptedAppendResult.ConflictError)), context, ct).ConfigureAwait(false);
            return await ReadAsync(realm, replayed, ct).ConfigureAwait(false);
        }

        var prechecked = hydrated.PrepareChange(request.Create, request.ExpectedRevision);
        if (!prechecked.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(prechecked.Error!);
        var resolved = await request.ResolveContent(ct).ConfigureAwait(false);
        if (!resolved.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(resolved.Error!);
        var bytes = resolved.Value!;
        var desired = kind.ReadName(bytes);
        NamedContentName? previous = null;
        if (!request.Create)
        {
            var current = hydrated.At(request.ExpectedRevision) ?? throw new InvalidOperationException(
                "A prepared revision change names the record's current revision.");
            previous = kind.ReadName(await content.ReadVerifiedAsync(realm, current.Content, ct).ConfigureAwait(false));
        }

        var authored = await content.WriteVerifiedAsync(realm, Uuid.CreateVersion4(), kind.AuthoredContentSchema,
            ImmutableContentStoreExtensions.JsonMediaType, bytes.ToArray(), ct).ConfigureAwait(false);
        var intent = new RecordNameChangeIntent(
            new DurableIntentHeader(request.LogicalId, stream, stream, stream, request.Actor, Episode,
                provenance.OriginalExpectedRevision, request.AuthoredIntentSha256, acceptedAt),
            kind.Kind, request.RecordId, request.Create ? 0UL : (ulong)request.ExpectedRevision,
            previous is null ? null : kind.ClaimStreamOf(request.TenantId, previous),
            kind.ClaimStreamOf(request.TenantId, desired), desired.Lifecycle, authored,
            RecordNameChangeIntent.NormalizationVersion);
        var intentRef = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), RecordNameChangeIntent.Schema,
            intent, ct).ConfigureAwait(false);
        var response = await FreezeAcceptedAsync(realm, request.LogicalId, acceptedAt, ct).ConfigureAwait(false);

        var accepted = await operations.ExecuteAsync(new NativeOperation<TRecord>(binding, Open,
            record => record.Acceptance,
            record =>
            {
                // The plan was made against this revision; a record that moved is stale and must be re-planned.
                var prepared = record.PrepareChange(request.Create, request.ExpectedRevision);
                return prepared.IsSuccess
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (TRecord record, ImmutableContentRef frozen) => accept(record, new AcceptAttempt(provenance,
                request.Create, request.ExpectedRevision, intentRef, frozen))), context, ct).ConfigureAwait(false);
        return await ReadAsync(realm, accepted, ct).ConfigureAwait(false);
    }

    /// <summary>The stable wire name of a change episode.</summary>
    const string Episode = "current";

    async Task<FirstResponseDescriptor> FreezeAcceptedAsync(string realm, Uuid operationId, DateTimeOffset acceptedAt,
        CancellationToken ct)
    {
        var response = new OperationAcceptedResponse(operationId, StatusUrl(realm, operationId), acceptedAt);
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), OperationAcceptedSchema, response, ct)
            .ConfigureAwait(false);
        return new FirstResponseDescriptor(AcceptedStatus, ImmutableContentStoreExtensions.JsonMediaType,
            OperationAcceptedSchema, new StandardResponseBody(body));
    }

    /// <summary>Schema of the frozen 202 response content.</summary>
    public const string OperationAcceptedSchema = "operation-accepted-response.v1";

    /// <summary>The status resource of an operation; the route is not mapped yet.</summary>
    public static string StatusUrl(string tenant, Uuid operationId) =>
        $"/api/v1/tenants/{tenant}/operations/{operationId}";

    async Task<Result<OperationAcceptedResponse>> ReadAsync(string realm, Result<AcceptedAppendResult> accepted,
        CancellationToken ct)
    {
        if (!accepted.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<OperationAcceptedResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body || descriptor.ResultSchema != OperationAcceptedSchema)
            throw new ImmutableContentIntegrityException("The accepted change froze an unexpected response.");
        return Result<OperationAcceptedResponse>.Success(
            await content.ReadJsonAsync<OperationAcceptedResponse>(realm, body.Content, ct).ConfigureAwait(false));
    }
}

/// <summary>
///     One authored change of a named record. <paramref name="ResolveContent" /> runs only for a new operation, after
///     the owner's accepted facts ruled out a replay, and returns the complete normalized content as JSON.
/// </summary>
public sealed record NamedRecordAdmissionRequest(Uuid TenantId, Uuid RecordId, Uuid LogicalId, bool Create,
    long ExpectedRevision, ActorReference Actor, string AuthoredIntentSha256,
    Func<CancellationToken, ValueTask<Result<ReadOnlyMemory<byte>>>> ResolveContent);

/// <summary>What a typed record owner needs to accept a change as its first native fact.</summary>
public sealed record AcceptAttempt(OperationProvenance Operation, bool Create, long ExpectedRevision,
    ImmutableContentRef Intent, ImmutableContentRef FirstResponse);
