using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Publishes exact immutable business content before accepting a bounded native fact.</summary>
public static class SignificantChangeContentStore
{
    public const string BodySchema = "significant-change-current.v1";
    public const string ChangeSchema = "significant-change-mutation.v1";
    public static SignificantChangeAuthorityHeader Header(SignificantChangeView state)
    {
        var current = state.Current ?? throw new ImmutableContentIntegrityException("The change has no current content.");
        return new(state.TenantId, state.ProgramId, state.SignificantChangeId, state.Revision,
            state.CurrentVersionNumber, current.ContentSha256, current.Content.Title, state.Status,
            current.Content.HandlingClass, current.Content.EffectiveOn, current.Content.EffectiveThrough,
            current.Content.OwnerMemberId, state.ActorMemberId, state.ActorDisplay, state.UpdatedAt, state.PeriodCloseEligible);
    }
    public static async ValueTask<FrozenSignificantChangeMutation> FreezeAsync(IImmutableContentStore content,
        SignificantChangeView? before, SignificantChangeRules rules, string kind, CancellationToken ct)
    {
        var after = rules.Current ?? throw new ImmutableContentIntegrityException("The change has no current content.");
        var realm = after.TenantId.ToString();
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.SignificantChangeId,
            BodySchema + ":" + AuthoredIntentDigest.Compute(after)), BodySchema, after, ct).ConfigureAwait(false);
        var mutation = new SignificantChangeMutation(kind, before is null ? null : AuthoredIntentDigest.Compute(before),
            AuthoredIntentDigest.Compute(after), after, rules.PendingChanges.Count > 0);
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), ChangeSchema, mutation, ct).ConfigureAwait(false);
        return new(Header(after), body, change, mutation, before, after);
    }
    public static DomainEvent Fact(FrozenSignificantChangeMutation frozen, long sequence,
        OperationProvenance operation, ImmutableContentRef first, ImmutableContentRef result)
    {
        var h = frozen.Header;
        var count = frozen.Mutation.Changed ? 1 : 0;
        var member = operation.Actor.Kind == "member" && Uuid.TryParse(operation.Actor.Id, out var actor) ? actor : h.ActorMemberId;
        return frozen.Mutation.Kind switch
        {
            nameof(RecordSignificantChange) => new SignificantChangeRecorded(h.TenantId, h.SignificantChangeId, h.Revision, member, h, frozen.Body, frozen.Change, sequence, count, operation, first, result, operation.AcceptedAt),
            nameof(ReviseSignificantChange) => new SignificantChangeRevised(h.TenantId, h.SignificantChangeId, h.Revision, member, h, frozen.Body, frozen.Change, sequence, count, operation, first, result, operation.AcceptedAt),
            nameof(AssessSignificantChange) => new SignificantChangeImpactAssessed(h.TenantId, h.SignificantChangeId, h.Revision, member, h, frozen.Body, frozen.Change, sequence, count, operation, first, result, operation.AcceptedAt),
            nameof(LinkImpactReviewWork) => new ImpactReviewWorkLinked(h.TenantId, h.SignificantChangeId, h.Revision, member, h, frozen.Body, frozen.Change, sequence, count, operation, first, result, operation.AcceptedAt),
            nameof(RecordSignificantChangeSystemDescriptionDecision) => new SignificantChangeSystemDescriptionDecisionRecorded(h.TenantId, h.SignificantChangeId, h.Revision, member, h, frozen.Body, frozen.Change, sequence, count, operation, first, result, operation.AcceptedAt),
            _ => throw new ImmutableContentIntegrityException("The change mutation has another command kind."),
        };
    }
    public static bool ValidMutation(SignificantChangeView? before, SignificantChangeView after,
        SignificantChangeMutation mutation)
    {
        var rules = new SignificantChangeRules(after.TenantId, after.ProgramId, after.SignificantChangeId);
        if (rules.ValidateState(after) is not null || mutation.AfterSha256 != AuthoredIntentDigest.Compute(after) ||
            mutation.BeforeSha256 != (before is null ? null : AuthoredIntentDigest.Compute(before)) ||
            AuthoredIntentDigest.Compute(mutation.After) != mutation.AfterSha256)
            return false;
        if (!mutation.Changed)
            return before is not null && mutation.BeforeSha256 == mutation.AfterSha256 &&
                mutation.Kind is nameof(ReviseSignificantChange) or nameof(AssessSignificantChange) or
                    nameof(LinkImpactReviewWork) or nameof(RecordSignificantChangeSystemDescriptionDecision);
        return SignificantChangeRules.VerifyTransition(before, after, mutation.Kind);
    }
    internal static void Apply(SignificantChangeRules rules, SignificantChangeMutation mutation)
    {
        if (!mutation.Changed)
            return;
        var state = mutation.After;
        object value = mutation.Kind switch
        {
            nameof(RecordSignificantChange) => new SignificantChangeRecordedBody(state.TenantId, state.SignificantChangeId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
            nameof(ReviseSignificantChange) => new SignificantChangeRevisedBody(state.TenantId, state.SignificantChangeId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
            nameof(AssessSignificantChange) => new SignificantChangeImpactAssessedBody(state.TenantId, state.SignificantChangeId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
            nameof(LinkImpactReviewWork) => new ImpactReviewWorkLinkedBody(state.TenantId, state.SignificantChangeId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
            nameof(RecordSignificantChangeSystemDescriptionDecision) => new SignificantChangeSystemDescriptionDecisionRecordedBody(state.TenantId, state.SignificantChangeId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
            _ => throw new ImmutableContentIntegrityException("The change mutation has another command kind."),
        };
        rules.Apply(value);
    }
}
