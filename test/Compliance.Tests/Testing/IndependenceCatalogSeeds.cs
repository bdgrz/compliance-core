using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
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
