using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using DescriptionOwner = Bdgrz.Compliance.Features.SystemDescription.Aggregates.SystemDescription;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
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
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
namespace Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;

/// <summary>Explicit native source fixture publication; never a production legacy adapter or permit.</summary>
static class DescriptionSeeds
{
    public static IImmutableContentStore Content() => new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
    public static async ValueTask<DescriptionOwner> WriteAsync(IAggregateWriter writer, IImmutableContentStore content, SystemDescriptionRules source)
    {
        var owner = new DescriptionOwner(source.TenantId, source.ProgramId, source.Id);
        var rules = new SystemDescriptionRules(source.TenantId, source.ProgramId, source.Id);
        await ApplyAsync(owner, rules, source.AppliedChanges, content).ConfigureAwait(false);
        await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System)).ConfigureAwait(false);
        source.ClearPendingChanges();
        source.SourceNativeSequence = owner.NativeSequence;
        source.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return owner;
    }
    public static async ValueTask<DescriptionOwner> AppendAsync(IAggregateWriter writer, IAggregateReader reader, IDomainEventReader events,
        IImmutableContentStore content, SystemDescriptionRules source)
    {
        var snapshot = await new SystemDescriptionReader(reader, events, content).ReadAsync(source.TenantId, source.ProgramId, source.Id).ConfigureAwait(false);
        await ApplyAsync(snapshot.Owner, snapshot.Rules, source.PendingChanges.ToArray(), content).ConfigureAwait(false);
        await writer.SaveAsync(snapshot.Owner, new RequestDispatchContext(RequestActor.System)).ConfigureAwait(false);
        source.ClearPendingChanges();
        source.SourceNativeSequence = snapshot.Owner.NativeSequence;
        source.SourcePhysicalPosition = snapshot.Owner.CommittedStreamPosition;
        return snapshot.Owner;
    }
    public static async ValueTask<DescriptionOwner> OwnerAsync(IImmutableContentStore content, SystemDescriptionRules source)
    {
        var owner = new DescriptionOwner(source.TenantId, source.ProgramId, source.Id);
        await ApplyAsync(owner, new SystemDescriptionRules(source.TenantId, source.ProgramId, source.Id), source.AppliedChanges, content).ConfigureAwait(false);
        return owner;
    }
    static async ValueTask ApplyAsync(DescriptionOwner owner, SystemDescriptionRules rules, IReadOnlyList<object> changes, IImmutableContentStore content)
    {
        foreach (var value in changes)
        {
            var before = rules.Read();
            rules.ClearPendingChanges();
            rules.RaiseEvent(value);
            var kind = value switch
            {
                SystemDescriptionDraftCreatedBody => nameof(CreateSystemDescriptionDraft),
                SystemDescriptionDraftRevisedBody => nameof(ReviseSystemDescriptionDraft),
                SystemDescriptionSourcesBoundBody => nameof(BindSystemDescriptionSources),
                SystemDescriptionSuccessorProposedBody => nameof(ProposeSystemDescriptionSuccessor),
                SystemDescriptionReviewedBody => nameof(ReviewSystemDescription),
                SystemDescriptionApprovedBody => nameof(ApproveSystemDescription),
                SystemDescriptionNoChangeRecordedBody => nameof(RecordSystemDescriptionNoChangeDecision),
                SystemDescriptionBoundBody => nameof(BindSystemDescriptionToExamination),
                _ => throw new InvalidOperationException("The description fixture has another mutation kind."),
            };
            var id = before.Revision == 0 ? owner.Id : Uuid.CreateVersion5(owner.Id, $"seed:{owner.NativeSequence + 1}:{kind}");
            var member = SystemDescriptionContent.Actor(value);
            var op = OwnerProvenance.For(owner, id, kind, ActorReference.ForMember(member, "Fixture actor"), owner.Id.ToString(),
                before.Revision == 0 ? null : unchecked((ulong)before.Revision), AuthoredIntentDigest.Compute(rules.Read()), SystemDescriptionContent.At(value)) with
            { FirstOwner = owner.Stream };
            var frozen = await SystemDescriptionContent.FreezeAsync(content, before, rules, kind, null, CancellationToken.None).ConfigureAwait(false);
            frozen = frozen with { SourceSnapshotContent = owner.SourceSnapshotContent };
            var response = await NativeContent.FreezeUnitAsync(content, owner.TenantId).ConfigureAwait(false);
            var first = await FirstResponses.FreezeAsync(content, owner.TenantId.ToString(), response.Response).ConfigureAwait(false);
            var result = owner.Append(SystemDescriptionFactFactory.Create(frozen, owner.NativeSequence + 1, op, first, response.Result),
                frozen, owner.NativeSequence, owner.CommittedStreamPosition);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
    }
}
