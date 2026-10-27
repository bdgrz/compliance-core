using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Changes.Persistence;
using Bdgrz.Compliance.Features.Changes.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
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
using Bdgrz.Compliance.Features.Changes.Aggregates;
using Bdgrz.Compliance.Features.Changes.Commands;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Changes.Tests;

/// <summary>Publishes real verified native facts for rule-arranged fixture histories; never a production compatibility path.</summary>
static class ChangeSeeds
{
    public static IImmutableContentStore Content() => new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public static async ValueTask<SignificantChange> WriteAsync(IAggregateWriter writer, IImmutableContentStore content,
        SignificantChangeRules source)
    {
        var owner = await OwnerAsync(content, source).ConfigureAwait(false);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System)).ConfigureAwait(false);
        return owner;
    }
    public static async ValueTask<SignificantChange> OwnerAsync(IImmutableContentStore content, SignificantChangeRules source)
    {
        var owner = new SignificantChange(source.TenantId, source.ProgramId, source.Id);
        var rules = new SignificantChangeRules(source.TenantId, source.ProgramId, source.Id);
        foreach (var change in source.AppliedChanges)
        {
            var before = rules.Current;
            rules.ClearPendingChanges();
            rules.RaiseEvent(change);
            var state = rules.Current!;
            var kind = change switch
            {
                SignificantChangeRecordedBody => nameof(RecordSignificantChange),
                SignificantChangeRevisedBody => nameof(ReviseSignificantChange),
                SignificantChangeImpactAssessedBody => nameof(AssessSignificantChange),
                ImpactReviewWorkLinkedBody => nameof(LinkImpactReviewWork),
                SignificantChangeSystemDescriptionDecisionRecordedBody => nameof(RecordSignificantChangeSystemDescriptionDecision),
                _ => throw new InvalidOperationException("The fixture has another rule mutation."),
            };
            var memberCommand = kind is nameof(RecordSignificantChange) or nameof(ReviseSignificantChange) or nameof(AssessSignificantChange);
            var actor = memberCommand ? ActorReference.ForMember(state.ActorMemberId, state.ActorDisplay) : ActorReference.ForSystemProcess("fixture", "fixture");
            var id = before is null ? source.Id : Uuid.CreateVersion5(source.Id, $"seed:{owner.NativeSequence + 1}:{kind}");
            var op = OwnerProvenance.For(owner, id, kind, actor, source.Id.ToString(),
                before is not null && memberCommand ? unchecked((ulong)before.Revision) : null,
                AuthoredIntentDigest.Compute(state), state.UpdatedAt) with
            { FirstOwner = owner.Stream };
            var frozen = await SignificantChangeContentStore.FreezeAsync(content, before, rules, kind, CancellationToken.None).ConfigureAwait(false);
            var response = await NativeContent.FreezeUnitAsync(content, source.TenantId).ConfigureAwait(false);
            var first = await FirstResponses.FreezeAsync(content, source.TenantId.ToString(), response.Response).ConfigureAwait(false);
            var result = owner.Append(SignificantChangeContentStore.Fact(frozen, owner.NativeSequence + 1, op, first, response.Result),
                frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Error!.Message);
        }
        return owner;
    }
}
