using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Closes a provider gap once, only against a verified newer or different source. The command id is the logical
///     identity: an exact retry replays the original result even after later facts, and a changed actor, intent or
///     expected revision under the same id conflicts. A closure never erases an already admitted risk link.
/// </summary>
public sealed class CloseProviderCoverageGapHandler(NativeOperationExecutor operations, IAggregateReader reader,
    AssuranceReferences references, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CloseProviderCoverageGap, ProviderCoverageGapRegistration>
{
    const string CommandKind = nameof(CloseProviderCoverageGap);
    const string Episode = "current";

    public async ValueTask<Result<ProviderCoverageGapRegistration>> HandleAsync(
        IRequestContext<CloseProviderCoverageGap> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderId == Uuid.Empty ||
            request.GapId == Uuid.Empty || request.ExpectedRevision < 1)
            return Fail(RequestErrorKind.Validation,
                "A provider coverage gap closure requires nonempty identities and the expected revision.");
        if (ProviderCoverageGapRules.ClosureInputError(request.Content) is { } inputError)
            return Fail(RequestErrorKind.Validation, inputError);
        var normalized = ProviderCoverageGapRules.Normalize(request.Content);
        var actor = ProviderActor.From(context);
        var now = clock.GetUtcNow();
        var provenance = Owner(request).Provenance(request.CommandId, CommandKind, actor, Episode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request with { Content = normalized }), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var prior = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? contentRef = null;
        ImmutableContentRef? resultRef = null;
        ProviderCoverageGapContent? recorded = null;
        if (prior.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct)
                .ConfigureAwait(false);
            if (!provider.IsSuccess)
                return Result<ProviderCoverageGapRegistration>.Failure(provider.Error);
            if (!prior.Exists)
                return Fail(RequestErrorKind.NotFound, "The provider coverage gap was not found.");
            try
            {
                recorded = await ProviderAssuranceContent.ReadAsync<ProviderCoverageGapContent>(content,
                    request.TenantId, prior.Recorded!.ContentContent, ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Result<ProviderCoverageGapRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The recorded provider coverage gap cannot be verified: {exception.Message}", true));
            }
            var prepared = prior.PrepareClose(request.ExpectedRevision, recorded, normalized);
            if (!prepared.IsSuccess)
                return Result<ProviderCoverageGapRegistration>.Failure(prepared.Error!);
            var source = await references.CoverageGapSourceAsync(request.TenantId, provider.Value, recorded.ServiceId,
                normalized.SourceKind, normalized.SourceId, normalized.SourceRevision, ct).ConfigureAwait(false);
            if (!source.IsSuccess)
                return Result<ProviderCoverageGapRegistration>.Failure(source.Error);
            contentRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                ProviderAssuranceContent.GapClosedSchema, normalized, ct).ConfigureAwait(false);
            resultRef = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
                ProviderAssuranceContent.GapRegistrationSchema,
                new ProviderCoverageGapRegistration(request.GapId, request.ExpectedRevision + 1), ct)
                .ConfigureAwait(false);
            response = ProviderAssuranceContent.Completed(resultRef, ProviderAssuranceContent.GapRegistrationSchema);
        }
        var accepted = await operations.ExecuteAsync(new NativeOperation<ProviderAssuranceGap>(binding,
            () => Owner(request), owner => owner.Acceptance,
            owner =>
            {
                var checkedClose = recorded is null
                    ? Result.Failure(AcceptedAppendResult.ConflictError)
                    : owner.PrepareClose(request.ExpectedRevision, recorded, normalized);
                return checkedClose.IsSuccess && response is not null
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(checkedClose.Error!);
            },
            (ProviderAssuranceGap owner, ImmutableContentRef frozen) => owner.Close(provenance,
                request.ExpectedRevision, recorded!, normalized, contentRef!, actor, now, frozen, resultRef!)),
            context, ct).ConfigureAwait(false);
        return await ProviderAssuranceContent.ReadFirstResponseAsync<ProviderCoverageGapRegistration>(content,
            request.TenantId, ProviderAssuranceContent.GapRegistrationSchema, accepted, ct).ConfigureAwait(false);
    }

    static ProviderAssuranceGap Owner(CloseProviderCoverageGap request) =>
        new(request.TenantId, request.ProviderId, request.GapId, request.CommandId);

    static Result<ProviderCoverageGapRegistration> Fail(RequestErrorKind kind, string message) =>
        Result<ProviderCoverageGapRegistration>.Failure(new RequestError(kind, message));
}
