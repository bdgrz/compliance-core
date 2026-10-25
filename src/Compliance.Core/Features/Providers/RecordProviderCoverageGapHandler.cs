using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Records a provider gap as the first fact of its own owner. The gap id is the logical identity: an exact retry
///     replays the original result, and a changed actor or intent under the same id conflicts. The provider, service
///     and source are observed only for a new operation.
/// </summary>
public sealed class RecordProviderCoverageGapHandler(NativeOperationExecutor operations, AssuranceReferences references, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordProviderCoverageGap, ProviderCoverageGapRegistration>
{
    const string CommandKind = nameof(RecordProviderCoverageGap);
    const string Episode = "initial";

    public ValueTask<Result<ProviderCoverageGapRegistration>> HandleAsync(
        IRequestContext<RecordProviderCoverageGap> context, CancellationToken ct) =>
        ProviderGapFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<ProviderCoverageGapRegistration>> HandleCoreAsync(
        IRequestContext<RecordProviderCoverageGap> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.GapId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderId == Uuid.Empty)
            return Fail(RequestErrorKind.Validation, "A provider coverage gap requires nonempty identities.");
        if (ProviderCoverageGapRules.InputError(request.Content) is { } inputError)
            return Fail(RequestErrorKind.Validation, inputError);
        var normalized = ProviderCoverageGapRules.Normalize(request.Content);
        var actor = ProviderActor.From(context);
        var now = clock.GetUtcNow();
        var provenance = Owner(request).Provenance(request.GapId, CommandKind, actor, Episode, null,
            AuthoredIntentDigest.Compute(request with { Content = normalized }), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? contentRef = null;
        ImmutableContentRef? resultRef = null;
        var providerRevision = 0L;
        ProviderGapPreparedCut? preparedCut = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<ProviderAssuranceGap>(binding,
            () => Owner(request), owner => owner.Acceptance,
            async (owner, token) =>
            {
                preparedCut = ProviderGapPreparedCut.Capture(owner);
                var prepared = owner.PrepareRecord();
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, token)
                    .ConfigureAwait(false);
                if (!provider.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(provider.Error);
                var source = await references.CoverageGapSourceAsync(request.TenantId, provider.Value,
                    normalized.ServiceId, normalized.SourceKind, normalized.SourceId, normalized.SourceRevision, token)
                    .ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(source.Error);
                providerRevision = provider.Value.Revision;
                contentRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                    ProviderAssuranceContent.GapRecordedSchema, normalized, token).ConfigureAwait(false);
                resultRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                    ProviderAssuranceContent.GapRegistrationSchema,
                    new ProviderCoverageGapRegistration(request.GapId, 1), token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(ProviderAssuranceContent.Completed(resultRef, ProviderAssuranceContent.GapRegistrationSchema));
            },
            (ProviderAssuranceGap owner, ImmutableContentRef frozen) =>
            {
                var guarded = owner.PrepareRecord();
                if (!guarded.IsSuccess)
                    return guarded;
                return preparedCut is null || !preparedCut.Matches(owner) ? ProviderGapPreparedCut.Changed()
                    : owner.Record(provenance, providerRevision, contentRef!, actor, now, frozen, resultRef!);
            }), context, ct).ConfigureAwait(false);
        return await ProviderAssuranceContent.ReadFirstResponseAsync<ProviderCoverageGapRegistration>(content,
            request.TenantId, ProviderAssuranceContent.GapRegistrationSchema, accepted, ct).ConfigureAwait(false);
    }

    static ProviderAssuranceGap Owner(RecordProviderCoverageGap request) =>
        new(request.TenantId, request.ProviderId, request.GapId, request.GapId);

    static Result<ProviderCoverageGapRegistration> Fail(RequestErrorKind kind, string message) =>
        Result<ProviderCoverageGapRegistration>.Failure(new RequestError(kind, message));
}
