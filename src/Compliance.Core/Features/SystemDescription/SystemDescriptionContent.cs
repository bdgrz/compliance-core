using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public static class SystemDescriptionContent
{
    public const string CurrentSchema = "system-description-current-version.v1";
    public static async ValueTask<FrozenSystemDescriptionMutation> FreezeAsync(IImmutableContentStore content,
        SystemDescriptionView before, SystemDescriptionRules rules, string kind, object? receipt, CancellationToken ct,
        SystemDescriptionCurrentAuthority? beforeAuthority = null)
    {
        var after = rules.Read();
        var mutation = rules.PendingChanges.Count == 1 ? rules.PendingChanges[0] :
            rules.PendingChanges.Count == 0 && receipt is not null ? receipt :
            throw new ImmutableContentIntegrityException("One description command requires one exact business mutation or verified no-op receipt.");
        var current = after.CurrentVersion ?? throw new ImmutableContentIntegrityException("The description has no current version.");
        var reference = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion5(after.SystemDescriptionId,
            CurrentSchema + ":" + AuthoredIntentDigest.Compute(current)), CurrentSchema, current, ct).ConfigureAwait(false);
        var payload = await WritePayloadAsync(content, after.TenantId, mutation, ct).ConfigureAwait(false);
        if (beforeAuthority is null && before.CurrentVersion is { } previous)
        {
            var priorReference = await content.WriteJsonAsync(before.TenantId.ToString(), Uuid.CreateVersion5(before.SystemDescriptionId,
                CurrentSchema + ":" + AuthoredIntentDigest.Compute(previous)), CurrentSchema, previous, ct).ConfigureAwait(false);
            beforeAuthority = SystemDescriptionAuthority.FromView(before, priorReference);
        }
        var authority = SystemDescriptionAuthority.Advance(beforeAuthority, before, after, mutation, rules.PendingChanges.Count == 1, reference);
        var authorityContent = await NativeContent.WriteAsync(content, after.TenantId, SystemDescriptionAuthority.Schema, authority, ct).ConfigureAwait(false);
        return new(before, after, SystemDescriptionAuthority.Header(after, authority), reference, payload, mutation, kind, rules.PendingChanges.Count == 1)
        { BeforeAuthority = beforeAuthority, Authority = authority, AuthorityContent = authorityContent };
    }
    static Task<ImmutableContentRef> WritePayloadAsync(IImmutableContentStore store, Uuid tenant, object mutation, CancellationToken ct) => mutation switch
    {
        SystemDescriptionDraftCreatedBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionDraftCreated.VersionContent.v1", item.Version, ct),
        SystemDescriptionDraftRevisedBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionDraftRevised.VersionContent.v1", item.Version, ct),
        SystemDescriptionSourcesBoundBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionSourcesBound.VersionContent.v1", item.Version, ct),
        SystemDescriptionSuccessorProposedBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionSuccessorProposed.VersionContent.v1", item.Version, ct),
        SystemDescriptionReviewedBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionReviewed.ReviewContent.v1", item.Review, ct),
        SystemDescriptionApprovedBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionApproved.ApprovalContent.v1", item.Approval, ct),
        SystemDescriptionNoChangeRecordedBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionNoChangeRecorded.DecisionContent.v1", item.Decision, ct),
        SystemDescriptionBoundBody item => NativeContent.WriteAsync(store, tenant, "SystemDescriptionBound.BindingContent.v1", item.Binding, ct),
        _ => throw new ImmutableContentIntegrityException("The description payload has another mutation kind."),
    };
    public static async ValueTask<object> ReadMutationAsync(IImmutableContentStore store, DomainEvent value, CancellationToken ct)
    {
        var f = value as ISystemDescriptionFact ?? throw new ImmutableContentIntegrityException("The source is not a native description fact.");
        var realm = f.TenantId.ToString();
        return value switch
        {
            SystemDescriptionDraftCreated item => new SystemDescriptionDraftCreatedBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionVersionView>(store, realm, item.VersionContent, "SystemDescriptionDraftCreated.VersionContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            SystemDescriptionDraftRevised item => new SystemDescriptionDraftRevisedBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionVersionView>(store, realm, item.VersionContent, "SystemDescriptionDraftRevised.VersionContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            SystemDescriptionSourcesBound item => new SystemDescriptionSourcesBoundBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionVersionView>(store, realm, item.VersionContent, "SystemDescriptionSourcesBound.VersionContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            SystemDescriptionSuccessorProposed item => new SystemDescriptionSuccessorProposedBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionVersionView>(store, realm, item.VersionContent, "SystemDescriptionSuccessorProposed.VersionContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            SystemDescriptionReviewed item => new SystemDescriptionReviewedBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionReviewView>(store, realm, item.ReviewContent, "SystemDescriptionReviewed.ReviewContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            SystemDescriptionApproved item => new SystemDescriptionApprovedBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionApprovalView>(store, realm, item.ApprovalContent, "SystemDescriptionApproved.ApprovalContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            SystemDescriptionNoChangeRecorded item => new SystemDescriptionNoChangeRecordedBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionNoChangeDecisionView>(store, realm, item.DecisionContent, "SystemDescriptionNoChangeRecorded.DecisionContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            SystemDescriptionBound item => new SystemDescriptionBoundBody(f.TenantId, f.ProgramId, f.SystemDescriptionId, f.AggregateRevision,
                await ReadAsync<SystemDescriptionBindingView>(store, realm, item.BindingContent, "SystemDescriptionBound.BindingContent.v1", ct).ConfigureAwait(false), f.OccurredAt),
            _ => throw new ImmutableContentIntegrityException("The native description source has another mutation kind."),
        };
    }
    static Task<T> ReadAsync<T>(IImmutableContentStore content, string realm, ImmutableContentRef reference, string schema, CancellationToken ct) where T : notnull =>
        reference.Schema == schema ? content.ReadJsonAsync<T>(realm, reference, ct) :
            throw new ImmutableContentIntegrityException("The description payload has another selected content schema.");
    public static ImmutableContentRef Payload(ISystemDescriptionFact fact) => fact switch
    {
        SystemDescriptionDraftCreated item => item.VersionContent,
        SystemDescriptionDraftRevised item => item.VersionContent,
        SystemDescriptionSourcesBound item => item.VersionContent,
        SystemDescriptionSuccessorProposed item => item.VersionContent,
        SystemDescriptionReviewed item => item.ReviewContent,
        SystemDescriptionApproved item => item.ApprovalContent,
        SystemDescriptionNoChangeRecorded item => item.DecisionContent,
        SystemDescriptionBound item => item.BindingContent,
        _ => throw new ImmutableContentIntegrityException("The native description source has another payload kind."),
    };
    public static bool Verify(FrozenSystemDescriptionMutation mutation)
    {
        try
        { return VerifyCore(mutation); }
        catch (InvalidOperationException) { return false; }
        catch (ImmutableContentIntegrityException) { return false; }
    }
    static bool VerifyCore(FrozenSystemDescriptionMutation mutation)
    {
        var before = mutation.Before;
        var rules = SystemDescriptionRules.FromView(before);
        var at = At(mutation.Mutation);
        var failure = mutation.Mutation switch
        {
            SystemDescriptionDraftCreatedBody item => rules.Create(item.Version.SystemDescriptionVersionId, item.Version.Scope, item.Version.Content,
                item.Version.AuthorMemberId, item.Version.AuthorDisplay, at),
            SystemDescriptionDraftRevisedBody item => rules.Revise(before.Revision, item.Version.SystemDescriptionVersionId, item.Version.Content, item.Version.ChangeReason,
                item.Version.AuthorMemberId, item.Version.AuthorDisplay, at),
            SystemDescriptionSourcesBoundBody item => rules.BindSources(before.Revision, item.Version.SystemDescriptionVersionId, item.Version.Content.Sources,
                item.Version.AuthorMemberId, item.Version.AuthorDisplay, at),
            SystemDescriptionSuccessorProposedBody item => rules.ProposeSuccessor(before.Revision, item.Version.SystemDescriptionVersionId, item.Version.Content, item.Version.ChangeReason,
                item.Version.AuthorMemberId, item.Version.AuthorDisplay, at),
            SystemDescriptionReviewedBody item => rules.Review(item.Review.Version, item.Review.ReviewerMemberId, item.Review.ReviewerDisplay, item.Review.Outcome, item.Review.Comments,
                item.Review.ReviewedAt),
            SystemDescriptionApprovedBody item => rules.Approve(item.Approval.Version, item.Approval.ApproverMemberId, item.Approval.ApproverDisplay, item.Approval.Rationale, item.Approval.ApprovedAt),
            SystemDescriptionNoChangeRecordedBody item => rules.RecordNoChangeDecision(item.Decision.Version, item.Decision.ChangeId, item.Decision.Rationale,
                item.Decision.DecidedByMemberId, item.Decision.DecidedByDisplay, item.Decision.DecidedAt),
            SystemDescriptionBoundBody item => rules.BindToExamination(item.Binding.Version, item.Binding.ExaminationId, item.Binding.Purpose, item.Binding.SnapshotId,
                item.Binding.BoundByMemberId, item.Binding.BoundByDisplay, item.Binding.BoundAt),
            _ => throw new ImmutableContentIntegrityException("The description mutation has another rule kind."),
        };
        return failure is null && (rules.PendingChanges.Count > 0) == mutation.Changed &&
            mutation.After.CurrentVersion is { } current && SystemDescriptionAuthority.Matches(mutation.CurrentVersion, current, CurrentSchema) &&
            MatchesPayload(mutation) &&
            AuthoredIntentDigest.Compute(rules.Read()) == AuthoredIntentDigest.Compute(mutation.After) &&
            (mutation.BeforeAuthority is null ? before.Revision == 0 : SystemDescriptionAuthority.MatchesBefore(before, mutation.BeforeAuthority,
                mutation.Mutation is SystemDescriptionBoundBody)) &&
            SystemDescriptionAuthority.Equal(SystemDescriptionAuthority.Advance(mutation.BeforeAuthority, before, mutation.After,
                mutation.Mutation, mutation.Changed, mutation.CurrentVersion), mutation.Authority) &&
            SystemDescriptionAuthority.Header(mutation.After, mutation.Authority) == mutation.Header &&
            SystemDescriptionAuthority.Matches(mutation.AuthorityContent, mutation.Authority, SystemDescriptionAuthority.Schema);
    }
    static bool MatchesPayload(FrozenSystemDescriptionMutation frozen) => frozen.Mutation switch
    {
        SystemDescriptionDraftCreatedBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Version, "SystemDescriptionDraftCreated.VersionContent.v1"),
        SystemDescriptionDraftRevisedBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Version, "SystemDescriptionDraftRevised.VersionContent.v1"),
        SystemDescriptionSourcesBoundBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Version, "SystemDescriptionSourcesBound.VersionContent.v1"),
        SystemDescriptionSuccessorProposedBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Version, "SystemDescriptionSuccessorProposed.VersionContent.v1"),
        SystemDescriptionReviewedBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Review, "SystemDescriptionReviewed.ReviewContent.v1"),
        SystemDescriptionApprovedBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Approval, "SystemDescriptionApproved.ApprovalContent.v1"),
        SystemDescriptionNoChangeRecordedBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Decision, "SystemDescriptionNoChangeRecorded.DecisionContent.v1"),
        SystemDescriptionBoundBody item => SystemDescriptionAuthority.Matches(frozen.Payload, item.Binding, "SystemDescriptionBound.BindingContent.v1"),
        _ => false,
    };
    public static Uuid Actor(object mutation) => mutation switch
    {
        SystemDescriptionDraftCreatedBody item => item.Version.AuthorMemberId,
        SystemDescriptionDraftRevisedBody item => item.Version.AuthorMemberId,
        SystemDescriptionSourcesBoundBody item => item.Version.AuthorMemberId,
        SystemDescriptionSuccessorProposedBody item => item.Version.AuthorMemberId,
        SystemDescriptionReviewedBody item => item.Review.ReviewerMemberId,
        SystemDescriptionApprovedBody item => item.Approval.ApproverMemberId,
        SystemDescriptionNoChangeRecordedBody item => item.Decision.DecidedByMemberId,
        SystemDescriptionBoundBody item => item.Binding.BoundByMemberId,
        _ => throw new ImmutableContentIntegrityException("The description mutation has another actor kind."),
    };
    public static DateTimeOffset At(object mutation) => mutation switch
    {
        SystemDescriptionDraftCreatedBody item => item.Version.UpdatedAt,
        SystemDescriptionDraftRevisedBody item => item.Version.UpdatedAt,
        SystemDescriptionSourcesBoundBody item => item.Version.UpdatedAt,
        SystemDescriptionSuccessorProposedBody item => item.Version.UpdatedAt,
        SystemDescriptionReviewedBody item => item.Review.ReviewedAt,
        SystemDescriptionApprovedBody item => item.Approval.ApprovedAt,
        SystemDescriptionNoChangeRecordedBody item => item.Decision.DecidedAt,
        SystemDescriptionBoundBody item => item.Binding.BoundAt,
        _ => throw new ImmutableContentIntegrityException("The description mutation has another attribution kind."),
    };
}
