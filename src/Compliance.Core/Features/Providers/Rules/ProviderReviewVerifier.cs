using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Rules;

/// <summary>
///     Verifies a receiver's proven source: the frozen review intent is read from verified immutable content and the
///     admission or terminal fact is found on the authoritative review owner. The source reference only names the
///     fact; nothing the caller states is trusted until the owner confirms it.
/// </summary>
sealed class ProviderReviewVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>
    ///     Loads and verifies the admitted review named by a receiver command.
    ///     <paramref name="terminal" /> states that the source must be the recording or cancellation instead of the
    ///     admission.
    /// </summary>
    public async ValueTask<LoadedReview> LoadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid reviewId, bool terminal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (reviewId == Uuid.Empty || !Uuid.TryParse(source.Stream.Realm, out var tenantId) || tenantId == Uuid.Empty)
            return LoadedReview.Failed(ProviderReviewEffects.Permanent("The receiver names another review or tenant."));
        var owner = await reader.HydrateAsync(new ProviderAssuranceReview(tenantId, reviewId), ct)
            .ConfigureAwait(false);
        if (owner.Requested is not { } requested)
            return LoadedReview.Failed(ProviderReviewEffects.Transient("The admitted review is not visible yet."));
        var expected = terminal ? owner.Recorded ?? (DomainEvent?)owner.Canceled : requested;
        if (expected is null)
            return LoadedReview.Failed(ProviderReviewEffects.Transient(
                "Neither the recording nor the cancellation of the review is visible yet."));
        if (source.Stream != owner.Stream || expected.Metadata.EventId != source.EventId ||
            expected.Metadata.AggregateVersion != source.AggregateVersion)
            return LoadedReview.Failed(ProviderReviewEffects.Permanent("The source is not the expected fact of this review."));
        if (requested.Intent != intentReference)
            return LoadedReview.Failed(ProviderReviewEffects.Permanent("The command differs from the frozen review intent."));
        ProviderReviewIntent intent;
        IReadOnlyList<SourceUseRequirement> requirements;
        try
        {
            intent = await ProviderAssuranceContent.ReadAsync<ProviderReviewIntent>(content, tenantId,
                intentReference, ct).ConfigureAwait(false);
            requirements = await content.ReadSelectionAsync<SourceUseRequirement>(
                ProviderAssuranceContent.Realm(tenantId), intent.RequiredSourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return LoadedReview.Failed(ProviderReviewEffects.Permanent(
                $"The frozen review cannot be verified: {exception.Message}"));
        }
        if (intent.ReviewId != reviewId || intent.Header.RootOperationId != reviewId ||
            intent.Header.FirstOwner != owner.Stream ||
            intent.Header.AuthoredIntentSha256 != requested.Operation.AuthoredIntentSha256)
            return LoadedReview.Failed(ProviderReviewEffects.Permanent("The command differs from the frozen review intent."));
        return new LoadedReview(owner, intent, requirements, null);
    }

    /// <summary>Derives the verified source of an acquire receiver, or the closed reason it is already moot.</summary>
    public async ValueTask<Result<SourceUseSource>> AuthorizeSourceAsync(
        AuthorizeProviderAssuranceReportProviderReviewUse request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await LoadAsync(request.Source, request.Intent, request.OperationId, false, ct)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<SourceUseSource>.Failure(loaded.Error);
        var (owner, intent) = (loaded.Owner!, loaded.Intent!);
        var requirement = ProviderReviewEffects.RequirementOf(loaded.Requirements!, request.Target);
        if (requirement is null || request.Generation != ProviderReviewEffects.Generation ||
            intent.ProviderId != request.ProviderId || request.ReviewId != request.OperationId ||
            requirement.ContentEvent.EventId != request.SourceContentEventId ||
            requirement.ResourceRevision != request.ExpectedContentRevision ||
            requirement.Purpose != SourceUsePurpose.ProviderReview)
            return Result<SourceUseSource>.Failure(ProviderReviewEffects.Permanent(
                "The authorization differs from the frozen source use requirement."));
        var superseded = owner.Canceled is not null ? "The review was canceled."
            : owner.Recorded is not null ? "The review is already recorded."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            ProviderReviewEffects.UseKey(request.OperationId, owner.Stream, request.Target), requirement,
            nameof(AuthorizeProviderAssuranceReportProviderReviewUse), null, superseded)
        {
            RequiresAbort = owner.Canceled is not null,
        });
    }

    /// <summary>Derives the verified terminal source of a settle receiver and the disposition it requires.</summary>
    public async ValueTask<Result<SourceUseSource>> SettleSourceAsync(
        SettleProviderAssuranceReportProviderReviewUse request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await LoadAsync(request.Source, request.Intent, request.OperationId, true, ct)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<SourceUseSource>.Failure(loaded.Error);
        var (owner, intent) = (loaded.Owner!, loaded.Intent!);
        var requirement = ProviderReviewEffects.RequirementOf(loaded.Requirements!, request.Target);
        if (requirement is null || request.Generation != ProviderReviewEffects.Generation ||
            intent.ProviderId != request.ProviderId || request.ReviewId != request.OperationId)
            return Result<SourceUseSource>.Failure(ProviderReviewEffects.Permanent(
                "The settlement differs from the frozen source use requirement."));
        var disposition = owner.Recorded is not null
            ? SourceUseSettlementDisposition.IrreversibleDecision
            : SourceUseSettlementDisposition.Canceled;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            ProviderReviewEffects.UseKey(request.OperationId, owner.Stream, request.Target), requirement,
            nameof(SettleProviderAssuranceReportProviderReviewUse), disposition, null));
    }
}

/// <summary>A verified review of its owner, or the reason it cannot be verified.</summary>
sealed record LoadedReview(ProviderAssuranceReview? Owner, ProviderReviewIntent? Intent,
    IReadOnlyList<SourceUseRequirement>? Requirements, RequestError? Error)
{
    public static LoadedReview Failed(RequestError error) => new(null, null, null, error);
}
