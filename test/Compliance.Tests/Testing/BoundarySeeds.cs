using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Boundaries.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Aggregates;
using Bdgrz.Compliance.Features.Boundaries.Commands;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

static class BoundarySeeds
{
    public static Task WriteAsync(IAggregateWriter writer, IImmutableContentStore content, BoundaryRules rules,
        CancellationToken ct = default) => SaveAsync(null, writer, content, rules, ct);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        BoundaryRules rules, CancellationToken ct = default) => SaveAsync(reader, writer, content, rules, ct);
    public static Task<DomainEvent> FreezeAsync(IImmutableContentStore content, BoundaryRules after,
        IReadOnlyList<IBoundaryMutationContent> changes, OperationProvenance operation, long sequence, CancellationToken ct = default) =>
        FreezeAsync(content, after, new BoundaryMutationContent(changes), operation, sequence, ct);
    public static async Task<DomainEvent> FreezeAsync(IImmutableContentStore content, BoundaryRules after,
        BoundaryMutationContent mutation, OperationProvenance operation, long sequence, CancellationToken ct = default)
    {
        var frozen = await BoundaryContentFreezer.FreezeAsync(content, after, mutation, ct);
        var response = await BoundaryResponses.FreezeAsync(content, after, operation.CommandKind, ct);
        var first = await FirstResponses.FreezeAsync(content, after.TenantId.ToString(), response, ct);
        var result = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(response.ResultSchema, response.Body), ct);
        return BoundaryFactFactory.Create(operation.CommandKind, frozen.Header, sequence, operation,
            frozen.Change, frozen.Authority, first, result, mutation);
    }
    static async Task SaveAsync(IAggregateReader? reader, IAggregateWriter writer, IImmutableContentStore content,
        BoundaryRules rules, CancellationToken ct)
    {
        if (rules.PendingChanges.Count == 0)
            return;
        var changes = rules.PendingChanges.ToArray();
        var current = new BoundaryRules(rules.TenantId, rules.Id);
        foreach (var change in rules.AppliedChanges.Take(rules.AppliedChanges.Count - changes.Length))
            current.Apply(change);
        current.ClearPendingChanges();
        var owner = new SystemBoundary(rules.TenantId, rules.Id);
        if (reader is not null)
            owner = await reader.HydrateAsync(owner, ct);
        if (owner.Header is { } prior && current.Header(prior.Content) != prior || owner.Header is null && current.IsCreated)
            throw new InvalidOperationException("The boundary fixture preparation has another native source cut.");
        foreach (var change in changes)
        {
            var metadata = Metadata(change);
            var command = metadata.Command ?? Uuid.CreateVersion5(owner.Id, "fixture:" + metadata.Kind + ":" + AuthoredIntentDigest.Compute(change));
            if (change is BoundarySuccessorProposedContent successor)
                command = rules.SuccessorCommand(successor.DraftVersionId);
            var gate = change is BoundarySuccessorProposedContent ? current.Revision : metadata.Gate;
            var operation = OwnerProvenance.For(owner, command, metadata.Kind, ActorReference.ForMember(metadata.Member, metadata.Display),
                owner.Id.ToString(), gate is { } expected ? unchecked((ulong)expected) : null,
                AuthoredIntentDigest.Compute(change), metadata.At);
            var before = owner.Header is { } header ? current.Authority(header) : null;
            current.Apply(change);
            var mutation = new BoundaryMutationContent([change], rules.WaiversFor(change));
            var fact = await FreezeAsync(content, current, mutation, operation, owner.NativeSequence + 1, ct);
            var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition,
                mutation, before, current.Authority(((IBoundaryFact)fact).Header));
            Assert.True(result.IsSuccess, result.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
        if (ContentDigest.OfJson(current.CurrentContent!) != ContentDigest.OfJson(rules.CurrentContent!) || current.Revision != rules.Revision)
            throw new InvalidOperationException("The boundary fixture did not publish all prepared changes.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
    }
    static SeedMetadata Metadata(IBoundaryMutationContent change) => change switch
    {
        BoundaryDraftCreatedContent item => new(nameof(CreateBoundary), item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt, null, item.BoundaryId),
        BoundaryDraftRevisedContent item => new(nameof(ReviseBoundaryDraft), item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt, item.Revision - 1, null),
        BoundarySuccessorProposedContent item => new(nameof(ProposeBoundarySuccessor), item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt, null, null),
        BoundaryReviewedContent item => new(nameof(ReviewBoundary), item.ActorMemberId, item.ActorDisplay, item.DecidedAt, item.Revision, item.DecisionId),
        BoundaryApprovedContent item => new(nameof(ApproveBoundary), item.ActorMemberId, item.ActorDisplay, item.DecidedAt, item.Revision, item.ApprovalDecisionId),
        BoundaryDraftDiscardedContent item => new(nameof(DiscardBoundaryDraft), item.ActorMemberId, item.ActorDisplay, item.DiscardedAt, item.Revision, null),
        ResponsibilityAssigned item => new(nameof(AssignResponsibility), item.AssignedByMemberId, item.AssignedByDisplay, item.AssignedAt, item.Scope.Revision, item.AssignmentId),
        ResponsibilityRevoked item => new(nameof(RevokeResponsibility), item.RevokedByMemberId, item.RevokedByDisplay, item.RevokedAt, item.Scope.Revision, null),
        _ => throw new InvalidOperationException("The boundary fixture has another mutation kind."),
    };
    sealed record SeedMetadata(string Kind, Uuid Member, string Display, DateTimeOffset At, long? Gate, Uuid? Command);
}
