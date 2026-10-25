using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Binds an actual selected native use receipt to its original immutable terms, never to absent history.</summary>
static class AccessReviewSourceUseReceipts
{
    public static async ValueTask<Result<SourceUseAuthorizationIntent>?> ReadAsync(IImmutableContentStore content,
        AccessReviewItem owner, ReservationKey key, OperationProvenance parent, NativeFactReference source,
        ImmutableContentRef parentIntent, SourceUsePurpose purpose, bool settlement, bool requirePriorGrant,
        CancellationToken ct)
    {
        var entry = owner.Uses.Find(key);
        if (entry is null || requirePriorGrant && entry.GrantFact is null)
            return null;
        var fact = settlement ? entry.SettleFact : entry.GrantFact ?? entry.SettleFact;
        if (fact is null)
            return null;
        try
        {
            if (fact is not ISourceUseFact native || SourceUseFactKinds.Classify(fact) is not { } kind || kind.Purpose != purpose ||
                native.Owner != owner.Stream || native.Generation < 0 || (ulong)native.Generation != key.SourceGeneration ||
                native.Operation.Owner != owner.Stream || native.Operation.RootOperationId != key.RootOperationId ||
                native.Operation.FirstOwner != key.FirstOwner || native.Operation.AffectedOwner != owner.Stream ||
                native.Operation.ExpectedRevisionOwner != owner.Stream || native.Operation.CausationEventId != source.EventId)
                throw Invalid("The selected use receipt differs from its actual scope, purpose or source.");
            var expectedKind = (purpose, kind.Granted) switch
            {
                (SourceUsePurpose.BulkDecision, true) => nameof(ReserveAccessReviewItemDecision),
                (SourceUsePurpose.BulkDecision, false) => nameof(SettleAccessReviewItemDecision),
                (SourceUsePurpose.CampaignCompletion, true) => nameof(AuthorizeAccessReviewItemCampaignCompletionUse),
                (SourceUsePurpose.CampaignCompletion, false) => nameof(SettleAccessReviewItemCampaignCompletionUse),
                _ => throw Invalid("The selected receipt has another source-use purpose."),
            };
            if (native.Operation.CommandKind != expectedKind)
                throw Invalid("The selected use receipt has another native operation kind.");
            if (owner.Acceptance.SelectedFact is { } selected && !Same(selected, entry.GrantFact) && !Same(selected, entry.SettleFact))
                return Result<SourceUseAuthorizationIntent>.Failure(AcceptedAppendResult.ConflictError);
            var intent = await content.ReadJsonAsync<SourceUseAuthorizationIntent>(owner.Stream.Realm, native.Intent, ct)
                .ConfigureAwait(false);
            if (intent.Header is null || intent.Reservation != key || intent.ParentIntent != parentIntent ||
                intent.Requirement is null || intent.Requirement.SourceOwner != owner.Stream ||
                intent.Requirement.ContentEvent is null || intent.Requirement.RequiredFacts is null ||
                intent.Requirement.ContentEvent.Stream != owner.Stream || intent.Requirement.Purpose != purpose)
                throw Invalid("The recorded use intent differs from its exact selected key or parent intent.");
            AccessReviewIntentBindings.Require(intent.Header, parent);
            if (native.Operation.Actor != intent.Header.OriginalActor || native.Operation.Episode != intent.Header.Episode ||
                native.Operation.OriginalExpectedRevision != intent.Header.OriginalExpectedRevision ||
                native.Operation.AuthoredIntentSha256 != intent.Header.AuthoredIntentSha256)
                throw Invalid("The native use receipt differs from its original actor and request binding.");
            if (kind.Granted)
            {
                if (intent.Grant is not null || intent.Disposition is not null)
                    throw Invalid("A grant receipt cannot carry another grant or terminal disposition.");
            }
            else
            {
                var disposition = fact switch
                {
                    AccessReviewItemDecisionSettled value => value.Disposition,
                    AccessReviewItemCampaignCompletionUseSettled value => value.Disposition,
                    _ => throw Invalid("The selected terminal receipt has another source-use kind."),
                };
                if (intent.Grant != owner.Uses.GrantOf(key) || intent.Disposition != disposition)
                    throw Invalid("The settlement receipt differs from its actual original grant or disposition.");
            }
            ct.ThrowIfCancellationRequested();
            return Result<SourceUseAuthorizationIntent>.Success(intent);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<SourceUseAuthorizationIntent>.Failure(new RequestError(RequestErrorKind.Conflict,
                error.Message, error is ImmutableContentUnavailableException));
        }
    }

    static bool Same(DomainEvent actual, DomainEvent? expected) => expected is not null && actual.GetType() == expected.GetType() &&
        actual.Equals(expected) && actual.Metadata == expected.Metadata;

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
