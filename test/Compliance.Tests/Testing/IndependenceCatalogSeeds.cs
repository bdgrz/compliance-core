using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Publishes fixture values as actual immutable catalog bodies and selected native acceptance facts.</summary>
static class IndependenceCatalogSeeds
{
    public static Task WriteAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        IndependenceRuleCatalogRules rules) => SaveRulesAsync(reader, writer, content, rules, rules.AppliedChanges.ToArray());
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        IndependenceRuleCatalogRules rules) => SaveRulesAsync(reader, writer, content, rules, rules.PendingChanges.ToArray());
    public static Task WriteAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        IndependenceRuleRatificationRules rules) => SaveRatificationsAsync(reader, writer, content, rules, rules.AppliedChanges.ToArray());
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        IndependenceRuleRatificationRules rules) => SaveRatificationsAsync(reader, writer, content, rules, rules.PendingChanges.ToArray());
    static async Task SaveRulesAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        IndependenceRuleCatalogRules source, IReadOnlyList<object> changes)
    {
        foreach (var change in changes.Cast<IndependenceRulesRevisionContent>())
        {
            var owner = await reader.HydrateAsync(new IndependenceRuleCatalog(change.RequestId));
            var request = new ReviseIndependenceRules(change.RequestId, change.ExpectedSequence, change.Version.Content);
            var operation = OwnerProvenance.For(owner, change.RequestId, nameof(ReviseIndependenceRules), change.Version.Actor,
                owner.Id.ToString(), unchecked((ulong)change.ExpectedSequence), AuthoredIntentDigest.Compute(request), change.Version.RecordedAt)
                with
            { FirstOwner = owner.Stream };
            if (owner.Acceptance.SelectedFact is not null)
            {
                Assert.Equal(NativeAcceptanceDecision.Replayed, owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation)));
                continue;
            }
            var node = new IndependenceRuleVersionContent(owner.Stream, change.RequestId, change.ExpectedSequence + 1,
                IndependenceRecordValidation.Freeze(change.Version), owner.CurrentContent);
            var reference = await content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(), IndependenceCatalogContent.RuleSchema, node);
            var result = await IndependenceCatalogContent.FreezeResultAsync(content, IndependenceCatalogContent.RuleResultSchema, node.Version);
            var first = await FirstResponses.FreezeAsync(content, IndependenceCatalogContent.Realm, result.Response);
            var outcome = owner.Append(new IndependenceRulesRevised(IndependenceCatalogContent.Header(node), reference, owner.NativeSequence + 1,
                operation, first, result.Result), node, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(outcome.IsSuccess, outcome.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
        source.ClearPendingChanges();
    }
    static async Task SaveRatificationsAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        IndependenceRuleRatificationRules source, IReadOnlyList<object> changes)
    {
        var reads = new IndependenceRuleRatificationReader(reader, content);
        foreach (var change in changes.Cast<IndependenceRatificationRecordedContent>())
        {
            var owner = await reader.HydrateAsync(new IndependenceRuleRatificationCatalog(change.RequestId));
            var value = change.Ratification;
            var request = new RatifyIndependenceRuleVersion(change.RequestId, value.RatificationId, value.RuleVersion,
                value.ObservedRuleCatalogSequence, change.ExpectedSequence, value.RuleContentDigest, value.SourceReference, change.ExpectedSequence);
            var operation = OwnerProvenance.For(owner, change.RequestId, nameof(RatifyIndependenceRuleVersion), value.Actor,
                owner.Id.ToString(), unchecked((ulong)change.ExpectedSequence), AuthoredIntentDigest.Compute(request), value.RecordedAt) with
            { FirstOwner = owner.Stream };
            if (owner.Acceptance.SelectedFact is not null)
            {
                Assert.Equal(NativeAcceptanceDecision.Replayed, owner.Acceptance.Decide(AcceptedOperationBinding.Of(operation)));
                continue;
            }
            var current = await reads.ReadAsync(owner);
            var history = await reads.IdentityEvidenceAsync(current, value.RatificationId);
            var node = new IndependenceRatificationContent(owner.Stream, change.RequestId, change.ExpectedSequence + 1, value, owner.CurrentContent);
            var reference = await content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(), IndependenceCatalogContent.RatificationSchema, node);
            var result = await IndependenceCatalogContent.FreezeResultAsync(content, IndependenceCatalogContent.RatificationResultSchema, value);
            var first = await FirstResponses.FreezeAsync(content, IndependenceCatalogContent.Realm, result.Response);
            var outcome = owner.Append(new IndependenceRuleRatificationRecorded(IndependenceCatalogContent.Header(node), reference, owner.NativeSequence + 1,
                operation, first, result.Result), node, history, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(outcome.IsSuccess, outcome.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        }
        source.ClearPendingChanges();
    }
}
