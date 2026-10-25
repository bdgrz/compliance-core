using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds findings and corrective actions as real native facts on their owners, with the frozen intents, content and
///     first responses the admission handlers would write, but without any authorization or process. A seeded action is
///     admitted and materialized; a seeded closure is accepted and closed in one step, as the closure process leaves it.
/// </summary>
static class FindingSeed
{
    public static async Task RaiseAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid findingId,
        FindingSource source, string title, string description, string severity, string affectedScope, Uuid ownerMemberId,
        DateOnly dueOn, IReadOnlyList<FindingLink> links, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var finding = new FindingAggregate(tenantId, programId, findingId, findingId);
        var operation = FindingContent.Provenance(findingId, "RaiseFinding", finding.Stream, actor, null,
            new string('a', 64), at);
        var linksReference = await FindingContent.WriteAsync(content, tenantId, FindingContent.LinksSchema, links,
            CancellationToken.None);
        var (response, result) = await FindingContent.FreezeAsync(content, tenantId, FindingContent.RegistrationSchema,
            new FindingRegistration(findingId, 1), CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), response);
        Assert.True(finding.Raise(operation, new FindingRaiseTerms(source, title, description, severity, affectedScope,
            ownerMemberId, dueOn, actor, at, "manual", null), linksReference, first, result, null).IsSuccess);
        await writer.SaveAsync(finding, new RequestDispatchContext(RequestActor.System));
    }

    /// <summary>Records a triage revision at the current revision of the finding.</summary>
    public static async Task ReviseAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid findingId,
        string severity, Uuid ownerMemberId, string reason, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var finding = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new FindingAggregate(tenantId, programId, findingId));
        var commandId = Uuid.CreateVersion4();
        var operation = FindingContent.Provenance(commandId, "ReviseFinding", finding.Stream, actor,
            (ulong)finding.Revision, new string("f"[0], 64), at);
        var (response, result) = await FindingContent.FreezeAsync(content, tenantId, UnitResult.Schema, new UnitResult(),
            CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), response);
        Assert.True(finding.Revise(operation, new FindingRevision(severity, ownerMemberId, finding.DueOn,
            finding.AffectedScope!, "Updated current finding state.", reason, actor, at), first, result).IsSuccess);
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(finding,
            new RequestDispatchContext(RequestActor.System));
    }

    /// <summary>Admits and materializes an action, and completes it when evidence is given.</summary>
    public static async Task AddActionAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid findingId,
        Uuid actionId, string description, Uuid ownerMemberId, DateOnly dueOn, ActorReference actor, DateTimeOffset at,
        EvidenceReference? completionEvidence = null, Uuid? completedByMemberId = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var context = new RequestDispatchContext(RequestActor.System);
        var finding = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId, actionId));
        var request = new AddCorrectiveAction(actionId, tenantId, programId, findingId, finding.Revision, description,
            ownerMemberId, dueOn);
        var operation = FindingContent.Provenance(actionId, "AddCorrectiveAction", finding.Stream, actor,
            (ulong)finding.Revision, new string('b', 64), at);
        var authored = await FindingContent.WriteAsync(content, tenantId, FindingContent.ActionAuthoredSchema, request,
            CancellationToken.None);
        var proofs = await RoutedFinding.EmptyProofsAsync(content, tenantId, actionId, CancellationToken.None);
        var intent = await FindingContent.WriteAsync(content, tenantId, ChildAdmissionIntent.Schema,
            new ChildAdmissionIntent(FindingContent.Header(operation, at), findingId, (ulong)finding.Generation,
                AdmittedChildKind.CorrectiveAction, actionId, authored, proofs, (ulong)finding.Revision + 1),
            CancellationToken.None);
        var ack = await FindingContent.AcceptedResponseAsync(content, tenantId, actionId, "/operations/{operation_id}", at,
            CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), ack);
        Assert.True(finding.AdmitAction(operation, intent, first).IsSuccess);
        await writer.SaveAsync(finding, context);

        var action = new CorrectiveAction(tenantId, programId, findingId, actionId, actionId);
        var view = new CorrectiveActionView(actionId, description, ownerMemberId, dueOn, FindingStates.Open, actor, at,
            Revision: 1);
        var actionContent = await FindingContent.WriteAsync(content, tenantId, FindingContent.ActionSchema, view,
            CancellationToken.None);
        var materialization = Provenances.Child(actionId, "MaterializeCorrectiveAction", intent, operation, action.Stream, at);
        Assert.True(action.Materialize(materialization, actionContent, intent).IsSuccess);
        if (completionEvidence is not null)
        {
            var completion = FindingContent.Provenance(Uuid.CreateVersion4(), "CompleteCorrectiveAction", action.Stream,
                ActorReference.ForMember(completedByMemberId ?? ownerMemberId, "Owner"), 1, new string('c', 64), at);
            var evidence = await FindingContent.WriteAsync(content, tenantId, FindingContent.EvidenceSchema,
                (IReadOnlyList<EvidenceReference>)[completionEvidence], CancellationToken.None);
            var completionView = await FindingContent.FreezeAsync(content, tenantId, FindingContent.ViewSchema, view,
                CancellationToken.None);
            var completionFirst = await FirstResponses.FreezeAsync(content, tenantId.ToString(), completionView.Response);
            Assert.True(action.Complete(completion, "Access removed.", evidence, completedByMemberId ?? ownerMemberId,
                ActorReference.ForMember(completedByMemberId ?? ownerMemberId, "Owner"), at, completionFirst,
                completionView.Result).IsSuccess);
        }

        await writer.SaveAsync(action, context);
    }

    /// <summary>Accepts and closes the finding in one step, then optionally reopens it.</summary>
    public static async Task CloseAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid findingId,
        Uuid closureId, Uuid closerMemberId, EvidenceReference evidence, DateTimeOffset at, bool reopen = false)
    {
        await using var scope = provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var finding = await reader.HydrateAsync(new FindingAggregate(tenantId, programId, findingId, closureId));
        var closer = ActorReference.ForMember(closerMemberId, "Reviewer");
        var operation = FindingContent.Provenance(closureId, "CloseFinding", finding.Stream, closer,
            (ulong)finding.Revision, new string('d', 64), at);
        var decision = new ClosureDecision(closureId, programId, "Verified access is removed.", [evidence], "Verified.",
            closerMemberId, closer, at, null);
        var decisionReference = await FindingContent.WriteAsync(content, tenantId, FindingContent.DecisionSchema, decision,
            CancellationToken.None);
        var history = await FindingHistory.ReadAsync(scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), finding);
        var children = history.Admitted.Select(fact => new AdmittedChild(fact.Operation.LogicalId,
            NativeFactReference.Of(finding.Stream, fact))).ToArray();
        var childManifest = await ImmutableSelectionManifests.WriteAsync<AdmittedChild>(content, tenantId.ToString(),
            Uuid.CreateVersion4(), FindingContent.AdmittedChildSchema, children, SelectionOrder.CanonicalKey);
        var uses = await ImmutableSelectionManifests.WriteAsync<Bdgrz.Compliance.Features.SourceUse.SourceUseRequirement>(content,
            tenantId.ToString(), Uuid.CreateVersion4(), Bdgrz.Compliance.Features.SourceUse.SourceUseRequirement.Schema, [],
            SelectionOrder.CanonicalKey);
        var intent = await FindingContent.WriteAsync(content, tenantId, ClosureIntent.Schema, new ClosureIntent(
            FindingContent.Header(operation, at), ClosureParentKind.Finding, findingId, (ulong)finding.Generation,
            childManifest, decisionReference, uses, null), CancellationToken.None);
        var ack = await FindingContent.AcceptedResponseAsync(content, tenantId, closureId, "/operations/{operation_id}", at,
            CancellationToken.None);
        Assert.True(finding.AcceptClosure(operation, intent,
            await FirstResponses.FreezeAsync(content, tenantId.ToString(), ack)).IsSuccess);
        var closureView = new FindingClosureView(closureId, decision.VerificationRationale, [evidence], decision.Rationale,
            closerMemberId, closer, at, null);
        var closureContent = await FindingContent.WriteAsync(content, tenantId, FindingContent.ClosureSchema, closureView,
            CancellationToken.None);
        var proofs = await FindingContent.WriteAsync(content, tenantId, FindingContent.ProofsSchema,
            (IReadOnlyList<ClosureActionProof>)[], CancellationToken.None);
        var result = await FindingContent.WriteAsync(content, tenantId, "mutation-result-content.v1",
            new MutationResultContent(FindingContent.ViewSchema, new StandardResponseBody(closureContent)),
            CancellationToken.None);
        var closed = Provenances.Child(Uuid.CreateVersion4(), "CompleteFindingClosure", intent, operation, finding.Stream, at, finding.PendingClosure!.Metadata.EventId);
        Assert.True(finding.CompleteClosure(closed, closureContent, proofs, intent, result).IsSuccess);
        if (reopen)
        {
            var reopening = FindingContent.Provenance(Uuid.CreateVersion4(), "ReopenFinding", finding.Stream, closer,
                (ulong)finding.Revision, new string('e', 64), at);
            var reopenFirst = await FirstResponses.FreezeAsync(content, tenantId.ToString(), FindingContent.OkResponse(
                FindingContent.ViewSchema, closureContent));
            Assert.True(finding.Reopen(reopening, "More accounts found.", closer, at, reopenFirst, result).IsSuccess);
        }

        await writer.SaveAsync(finding, new RequestDispatchContext(RequestActor.System));
    }

    static class Provenances
    {
        public static OperationProvenance Child(Uuid commandId, string kind, ImmutableContentRef _,
            OperationProvenance root, EventStreamAddress owner, DateTimeOffset at, Uuid? cause = null) => new(commandId, root.RootOperationId,
            cause, kind, root.Actor, owner, root.Episode, root.OriginalExpectedRevision, root.AuthoredIntentSha256, at, owner,
            owner);
    }
}
