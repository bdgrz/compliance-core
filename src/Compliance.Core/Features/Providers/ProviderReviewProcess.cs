using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     The ordered provider review process. Acquire holds the exact report revision the review pinned at admission,
///     complete is the irreversible recording of the review, and settle releases the report. A receiver that proves
///     the review can never proceed rejects it: the abort receiver then cancels it before the recording and settle
///     fences the report use, held or not. A review that cites external evidence requires no report, so its acquire
///     and settle steps are skipped.
/// </summary>
static class ProviderReviewProcess
{
    public const string Acquire = "acquire";
    public const string Complete = "complete";
    public const string Settle = "settle";

    public static ProcessDefinition Definition() => new(ProviderReviewEffects.ProcessName,
        [nameof(ProviderReviewRequested)], true,
        [
            new ProcessStageDefinition(Acquire, [nameof(AuthorizeProviderAssuranceReportProviderReviewUse)]),
            new ProcessStageDefinition(Complete, [nameof(CompleteProviderAssuranceReview)]),
            new ProcessStageDefinition(Settle, [nameof(SettleProviderAssuranceReportProviderReviewUse)]),
        ], [nameof(AbortProviderReview)], CreateReceiverAsync);

    static async ValueTask<IRequest<ReceiverOutcome>?> CreateReceiverAsync(ProcessStep step, IServiceProvider services,
        CancellationToken ct)
    {
        var record = step.Record;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var tenantId = Uuid.Parse(record.Key.Realm, System.Globalization.CultureInfo.InvariantCulture);
        var reviewId = record.Key.RootOperationId;
        var review = record.Source.Stream;
        ProviderReviewIntent intent;
        IReadOnlyList<SourceUseRequirement> requirements;
        try
        {
            intent = await ProviderAssuranceContent.ReadAsync<ProviderReviewIntent>(content, tenantId, record.Intent,
                ct).ConfigureAwait(false);
            requirements = await content.ReadSelectionAsync<SourceUseRequirement>(
                ProviderAssuranceContent.Realm(tenantId), intent.RequiredSourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The frozen review is not readable yet: {exception.Message}");
        }
        var aborting = record.State == ProcessState.Aborting;
        var requirement = requirements.SingleOrDefault();
        var generation = ProviderReviewEffects.Generation;
        switch (step.Receiver)
        {
            case nameof(AuthorizeProviderAssuranceReportProviderReviewUse) when requirement is not null:
                return new AuthorizeProviderAssuranceReportProviderReviewUse(step.CommandId, record.Source,
                    requirement.SourceOwner, reviewId, generation, null, record.Intent,
                    await ReportIdAsync(content, tenantId, intent, ct).ConfigureAwait(false),
                    requirement.ContentEvent.EventId, checked((long)requirement.ResourceRevision), intent.ProviderId, reviewId);
            case nameof(CompleteProviderAssuranceReview):
                return new CompleteProviderAssuranceReview(step.CommandId, record.Source, review, reviewId,
                    generation, null, record.Intent, reviewId,
                    await ProofsAsync(record, requirement, tenantId, content, ct).ConfigureAwait(false));
            case nameof(SettleProviderAssuranceReportProviderReviewUse) when requirement is not null:
                var terminal = record.Facts.TryGetValue(
                    aborting ? nameof(AbortProviderReview) : nameof(CompleteProviderAssuranceReview), out var fact)
                    ? fact
                    : throw new SourceFactVerificationException("The terminal review fact is not recorded in the process yet.");
                return new SettleProviderAssuranceReportProviderReviewUse(step.CommandId, terminal,
                    requirement.SourceOwner, reviewId, generation, null, record.Intent,
                    await ReportIdAsync(content, tenantId, intent, ct).ConfigureAwait(false), intent.ProviderId,
                    reviewId, aborting
                        ? SourceUseSettlementDisposition.Canceled
                        : SourceUseSettlementDisposition.IrreversibleDecision);
            case nameof(AbortProviderReview):
                var proof = await ProviderAssuranceContent.WriteAsync(content, tenantId,
                    ProviderAssuranceContent.FailureProofSchema,
                    new ProviderReviewFailureProof(reviewId, ProviderReviewFailureProof.SourceMismatch), ct,
                    Uuid.CreateVersion5(record.Source.EventId, ProviderAssuranceContent.FailureProofSchema))
                    .ConfigureAwait(false);
                return new AbortProviderReview(step.CommandId, review, reviewId, generation, null, proof);
            default:
                return null;
        }
    }

    static async Task<Uuid> ReportIdAsync(IImmutableContentStore content, Uuid tenantId, ProviderReviewIntent intent,
        CancellationToken ct)
    {
        try
        {
            var authored = await ProviderAssuranceContent.ReadAsync<RecordProviderReview>(content, tenantId,
                intent.AuthoredReview, ct).ConfigureAwait(false);
            return authored.Content.AssuranceReportId
                ?? throw new SourceFactVerificationException("The review names no report to hold.");
        }
        catch (ImmutableContentIntegrityException exception)
        {
            throw new SourceFactVerificationException($"The authored review is not readable yet: {exception.Message}");
        }
    }

    /// <summary>
    ///     The grant facts the acquire stage produced, in the order of the frozen requirement manifest. The content is
    ///     keyed by the root admission, so every step that needs it writes the identical bytes to the identical id.
    /// </summary>
    static async Task<ImmutableContentRef> ProofsAsync(ProcessRecord record, SourceUseRequirement? requirement,
        Uuid tenantId, IImmutableContentStore content, CancellationToken ct)
    {
        var proofs = new List<NativeFactReference>(1);
        if (requirement is not null)
            proofs.Add(record.Facts.TryGetValue(nameof(AuthorizeProviderAssuranceReportProviderReviewUse), out var fact)
                ? fact
                : throw new SourceFactVerificationException("The report use has not produced its grant yet."));
        return await ProviderAssuranceContent.WriteAsync(content, tenantId, ProviderReviewEffects.UseProofSchema,
            proofs.ToArray(), ct, Uuid.CreateVersion5(record.Source.EventId, ProviderReviewEffects.UseProofSchema))
            .ConfigureAwait(false);
    }
}
