using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Contracts;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Publishes real native fixtures from pure Policy preparations; production has no dual read or legacy owner path.</summary>
static class PolicySeeds
{
    public static Task WriteAsync(IAggregateWriter writer, IImmutableContentStore content, PolicyRules rules,
        CancellationToken ct = default) => SaveAsync(null, writer, content, rules, ct);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        PolicyRules rules, CancellationToken ct = default) => SaveAsync(reader, writer, content, rules, ct);

    public static Task<DomainEvent> FreezeAsync(IImmutableContentStore content, PolicyRules after,
        IPolicyMutationContent change, OperationProvenance operation, long nativeSequence, CancellationToken ct) =>
        FreezeAsync(content, after, change, operation, nativeSequence, null, ct);

    public static async Task<DomainEvent> FreezeAsync(IImmutableContentStore content, PolicyRules after,
        IPolicyMutationContent change, OperationProvenance operation, long nativeSequence,
        SeparationOfDutiesWaiverProof? waiver = null, CancellationToken ct = default)
    {
        var mutation = new PolicyMutationContent(change, waiver);
        var frozen = await PolicyContentStore.FreezeAsync(content, after, mutation, ct);
        var response = await PolicyResponses.FreezeAsync(content, after, operation.CommandKind, operation.LogicalId, ct);
        var first = await FirstResponses.FreezeAsync(content, after.TenantId.ToString(), response, ct);
        var result = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(response.ResultSchema, response.Body), ct);
        return PolicyFactFactory.Create(operation.CommandKind, frozen.Header, nativeSequence, operation,
            frozen.Change, frozen.Authority, first, result, mutation);
    }

    static async Task SaveAsync(IAggregateReader? reader, IAggregateWriter writer, IImmutableContentStore content,
        PolicyRules rules, CancellationToken ct)
    {
        if (rules.PendingChanges.Count == 0)
            return;
        var changes = rules.PendingChanges.ToArray();
        var current = new PolicyRules(rules.TenantId, rules.Id);
        foreach (var applied in rules.AppliedChanges.Take(rules.AppliedChanges.Count - changes.Length))
            current.Apply(applied);
        current.ClearPendingChanges();
        var owner = new Policy(rules.TenantId, rules.Id);
        if (reader is not null)
            owner = await reader.HydrateAsync(owner, ct);
        if (owner.Header is { } prior && current.Header(prior.Content) != prior || owner.Header is null && current.IsCreated)
            throw new InvalidOperationException("The Policy fixture preparation has another original native source cut.");
        foreach (var change in changes)
        {
            var metadata = Metadata(change);
            var logical = metadata.Id ?? Uuid.CreateVersion5(owner.Id,
                "fixture:" + metadata.Kind + ":" + AuthoredIntentDigest.Compute(change));
            var operation = OwnerProvenance.For(owner, logical, metadata.Kind,
                ActorReference.ForMember(metadata.Actor, metadata.Actor.ToString()), owner.Id.ToString(),
                metadata.Gate is { } gate ? unchecked((ulong)gate) : null, Authored(change, logical), metadata.At);
            var before = owner.Header is { } header ? current.Authority(header) : null;
            current.Apply(change);
            var waiver = rules.WaiverFor(change);
            var fact = await FreezeAsync(content, current, change, operation, owner.NativeSequence + 1, waiver, ct);
            var accepted = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition,
                new PolicyMutationContent(change, waiver), before, current.Authority(((IPolicyFact)fact).Header));
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
        if (PolicyRules.Hash(current.CurrentContent!) != PolicyRules.Hash(rules.CurrentContent!) || current.Revision != rules.Revision)
            throw new InvalidOperationException("The Policy fixture did not publish its complete native mutation sequence.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
    }

    static SeedMetadata Metadata(IPolicyMutationContent change) => change switch
    {
        PolicyDraftCreatedContent item => new(nameof(CreatePolicyDraft), item.ActorMemberId, item.ChangedAt, null, item.PolicyId),
        PolicyDraftRevisedContent item => new(item.PredecessorVersion is null ? nameof(RevisePolicyDraft) : nameof(ProposePolicySuccessor),
            item.ActorMemberId, item.ChangedAt, item.PredecessorVersion is null ? item.Revision - 1 : item.PredecessorVersion, null),
        PolicyReviewedContent item => new(nameof(ReviewPolicyDraft), item.ActorMemberId, item.DecidedAt, item.Revision, item.DecisionId),
        PolicyApprovedContent item => new(nameof(ApprovePolicy), item.ActorMemberId, item.DecidedAt, item.Revision, item.DecisionId),
        PolicyPeriodicReviewConfirmedContent item => new(nameof(ConfirmPolicyReview), item.ActorMemberId, item.DecidedAt, item.Version, item.DecisionId),
        PolicyRetirementProposedContent item => new(nameof(ProposePolicyRetirement), item.ActorMemberId, item.ProposedAt, item.Version, null),
        PolicyRetiredContent item => new(nameof(ApprovePolicyRetirement), item.ActorMemberId, item.DecidedAt, item.Revision, item.DecisionId),
        PolicyDraftDiscardedContent item => new(nameof(DiscardPolicyDraft), Uuid.Parse(item.Actor.Id, CultureInfo.InvariantCulture), item.DiscardedAt, item.Revision, null),
        _ => throw new InvalidOperationException("The Policy fixture has another business mutation kind."),
    };

    static string Authored(IPolicyMutationContent change, Uuid command) => change switch
    {
        PolicyDraftCreatedContent item => AuthoredIntentDigest.Compute(new CreatePolicyDraft(item.PolicyId, item.TenantId,
            item.ProgramId, item.Identifier, item.Content)),
        PolicyDraftRevisedContent item when item.PredecessorVersion is { } predecessor => AuthoredIntentDigest.Compute(new ProposePolicySuccessor(
            command, item.TenantId, item.ProgramId, item.PolicyId, predecessor, item.Content)),
        PolicyDraftRevisedContent item => AuthoredIntentDigest.Compute(new RevisePolicyDraft(command, item.TenantId, item.ProgramId,
            item.PolicyId, item.Revision - 1, item.Content)),
        PolicyReviewedContent item => AuthoredIntentDigest.Compute(new ReviewPolicyDraft(command, item.TenantId, item.ProgramId,
            item.PolicyId, item.Revision, item.Outcome, item.Rationale, item.SeparationOfDutiesWaiverId)),
        PolicyApprovedContent item => AuthoredIntentDigest.Compute(new ApprovePolicy(command, item.TenantId, item.ProgramId,
            item.PolicyId, item.Revision, item.AcceptedReviewDecisionId, item.EffectiveFrom, item.Major, item.Rationale,
            item.ImpactDigest, item.SeparationOfDutiesWaiverId)),
        PolicyPeriodicReviewConfirmedContent item => AuthoredIntentDigest.Compute(new ConfirmPolicyReview(command, item.TenantId, item.ProgramId,
            item.PolicyId, item.Version, item.Rationale)),
        PolicyRetirementProposedContent item => AuthoredIntentDigest.Compute(new ProposePolicyRetirement(command, item.TenantId, item.ProgramId,
            item.PolicyId, item.Version, item.EffectiveUntil, item.Rationale)),
        PolicyRetiredContent item => AuthoredIntentDigest.Compute(new ApprovePolicyRetirement(command, item.TenantId, item.ProgramId,
            item.PolicyId, item.Revision, item.AcceptedReviewDecisionId, item.Rationale, item.SeparationOfDutiesWaiverId)),
        PolicyDraftDiscardedContent item => AuthoredIntentDigest.Compute(new DiscardPolicyDraft(command, item.TenantId, item.ProgramId,
            item.PolicyId, item.Revision, item.Rationale)),
        _ => throw new InvalidOperationException("The Policy fixture has another authored intent kind."),
    };
    sealed record SeedMetadata(string Kind, Uuid Actor, DateTimeOffset At, long? Gate, Uuid? Id);
}
