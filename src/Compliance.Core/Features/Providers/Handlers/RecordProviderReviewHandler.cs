using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Admits a personal review as the root fact of its own owner and answers 202. Admission freezes the normalized
///     authored review, the exact report fact it relies on and the source uses it requires, after proving the
///     review rules against the current report: the same provider and evidence kind, a review not before the end of
///     the report period, and a stale report never supporting an acceptable conclusion. The review process then holds
///     the exact report revision and records the review. The review id is the logical identity: an exact retry
///     replays the original acknowledgment even after the report, provider or artifact changed.
/// </summary>
public sealed class RecordProviderReviewHandler(NativeOperationExecutor operations, IAggregateReader reader,
    AssuranceReferences references, ISourceFactResolver resolver, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RecordProviderReview, OperationAcceptedResponse>
{
    const string CommandKind = nameof(RecordProviderReview);
    const string Episode = "initial";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<RecordProviderReview> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Fail(RequestErrorKind.Forbidden, "Provider review requires personal HTTP submission.");
        var request = context.Request;
        if (request.ReviewId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProviderId == Uuid.Empty)
            return Fail(RequestErrorKind.Validation, "A provider review requires nonempty identities.");
        if (AssuranceRules.InputError(request.Content) is { } inputError)
            return Fail(RequestErrorKind.Validation, inputError);
        var normalized = AssuranceRules.Normalize(request.Content);
        if (AssuranceRules.Validate(normalized) is { } error)
            return Fail(RequestErrorKind.Validation, error);
        // The reviewer is always the authenticated member; a review is a personal sign-off.
        var actor = ProviderActor.From(context);
        var canonical = request with { Content = normalized };
        var now = clock.GetUtcNow();
        var provenance = Owner(request).Provenance(request.ReviewId, request.ReviewId, null, CommandKind, actor,
            Episode, null, AuthoredIntentDigest.Compute(canonical), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var prior = await reader.HydrateAsync(Owner(request), ct).ConfigureAwait(false);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? intent = null;
        if (prior.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var frozen = await FreezeAsync(canonical, prior, provenance, now, ct).ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(frozen.Error!);
            (intent, response) = frozen.Value;
        }
        var accepted = await operations.ExecuteAsync(new NativeOperation<ProviderAssuranceReview>(binding,
            () => Owner(request), owner => owner.Acceptance,
            owner =>
            {
                var checkedAdmit = owner.PrepareAdmit();
                return checkedAdmit.IsSuccess && response is not null
                    ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(checkedAdmit.IsSuccess
                        ? AcceptedAppendResult.ConflictError
                        : checkedAdmit.Error!);
            },
            (ProviderAssuranceReview owner, ImmutableContentRef acknowledgment) =>
                owner.Admit(provenance, intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await ProviderAssuranceContent.ReadFirstResponseAsync<OperationAcceptedResponse>(content,
            request.TenantId, ProviderAssuranceContent.AcceptedSchema, accepted, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(
        RecordProviderReview request, ProviderAssuranceReview review, OperationProvenance provenance,
        DateTimeOffset now, CancellationToken ct)
    {
        var prepared = review.PrepareAdmit();
        if (!prepared.IsSuccess)
            return Failed(prepared.Error!);
        var reviewContent = request.Content;
        if (reviewContent.ReviewedAt > DateOnly.FromDateTime(now.UtcDateTime))
            return Failed(new RequestError(RequestErrorKind.Validation, "A review cannot be dated after it is recorded."));
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Failed(provider.Error);
        var artifact = await references.ArtifactAsync(request.TenantId, reviewContent.Evidence, ct)
            .ConfigureAwait(false);
        if (!artifact.IsSuccess)
            return Failed(artifact.Error);
        if (provider.Value.Content.Materiality != "not_material" &&
            reviewContent.NextReviewDue > reviewContent.ReviewedAt.AddMonths(AssuranceRules.AnnualMonths))
            return Failed(new RequestError(RequestErrorKind.Validation,
                "A material or unclassified provider is reviewed at least annually (M0-D11)."));
        SourceFactLocator? locator = null;
        var requirements = new List<SourceUseRequirement>();
        if (reviewContent.AssuranceReportId is { } reportId)
        {
            var report = await reader.HydrateAsync(new ProviderAssuranceReport(request.TenantId, request.ProviderId,
                reportId), ct).ConfigureAwait(false);
            if (!report.Exists || report.LatestFact is null || report.LatestContent is null)
                return Failed(new RequestError(RequestErrorKind.NotFound,
                    "The assurance report was not found for this provider."));
            AssuranceReportContent reportContent;
            try
            {
                reportContent = await ProviderAssuranceContent.ReadAsync<AssuranceReportContent>(content,
                    request.TenantId, report.LatestContent, ct).ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException exception)
            {
                return Failed(new RequestError(RequestErrorKind.Conflict,
                    $"The assurance report cannot be verified: {exception.Message}", true));
            }
            if (reportContent.ReportKind != reviewContent.EvidenceKind)
                return Failed(new RequestError(RequestErrorKind.Validation,
                    "The evidence kind must match the assurance report kind."));
            if (reviewContent.ReviewedAt < reportContent.PeriodEnd)
                return Failed(new RequestError(RequestErrorKind.Validation,
                    "A review cannot precede the end of its report period."));
            if (reviewContent.Conclusion != "not_acceptable" &&
                ProviderAssuranceCoverage.Currency(reportContent, reviewContent.ReviewedAt) == "stale")
                return Failed(new RequestError(RequestErrorKind.Validation,
                    "A stale report cannot support an acceptable conclusion; record the review as not_acceptable."));
            var held = NativeFactReference.Of(report.Stream, report.LatestFact);
            try
            {
                locator = (await resolver.ResolveAsync(report.Stream, held.AggregateVersion - 1, ct)
                    .ConfigureAwait(false)).Locator;
            }
            catch (SourceFactVerificationException exception)
            {
                return Failed(new RequestError(RequestErrorKind.Conflict,
                    $"The assurance report fact cannot be proven yet: {exception.Message}", true));
            }
            if (locator.EventId != held.EventId)
                return Failed(new RequestError(RequestErrorKind.Conflict,
                    "The assurance report changed while the review was prepared; reload and retry.", true));
            requirements.Add(new SourceUseRequirement(report.Stream, held, (ulong)report.Revision,
                SourceUsePurpose.ProviderReview, report.LatestContent));
        }
        var authored = await ProviderAssuranceContent.WriteAsync(content, request.TenantId,
            ProviderReviewIntent.AuthoredSchema, request, ct).ConfigureAwait(false);
        var manifest = await content.WriteSelectionAsync(ProviderAssuranceContent.Realm(request.TenantId),
            Uuid.CreateVersion4(), SourceUseRequirement.Schema, requirements, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var header = new DurableIntentHeader(request.ReviewId, provenance.Owner, provenance.Owner, provenance.Owner,
            provenance.Actor, provenance.Episode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256,
            now);
        var intent = await ProviderAssuranceContent.WriteAsync(content, request.TenantId, ProviderReviewIntent.Schema,
            new ProviderReviewIntent(header, request.ReviewId, request.ProviderId, authored, locator, manifest, now),
            ct).ConfigureAwait(false);
        var response = await ProviderAssuranceContent.AcceptedAsync(content, request.TenantId, request.ReviewId, now,
            ct).ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent, response));
    }

    static Result<(ImmutableContentRef, FirstResponseDescriptor)> Failed(RequestError error) =>
        Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(error);

    static ProviderAssuranceReview Owner(RecordProviderReview request) =>
        new(request.TenantId, request.ReviewId, request.ReviewId);

    static Result<OperationAcceptedResponse> Fail(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
