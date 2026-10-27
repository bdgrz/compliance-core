using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class ControlProjectionFacts
{
    readonly Dictionary<(Uuid Tenant, Uuid Control), ControlRules> _rules = [];
    readonly Dictionary<(Uuid Tenant, Uuid Control), long> _sequences = [];
    public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new InMemoryKvClient());
    public Task<ControlDraftCreated> FreezeAsync(ControlDraftCreatedContent change) =>
        FreezeAsync<ControlDraftCreated>(change, change.TenantId, change.ProgramId, change.ControlId,
            change.CreateRequestId, change.ActorMemberId, change.ActorDisplay, change.ChangedAt, "CreateControlDraft");

    public Task<ControlDraftRevised> FreezeAsync(ControlDraftRevisedContent change) =>
        FreezeAsync<ControlDraftRevised>(change, change.TenantId, change.ProgramId, change.ControlId,
            Uuid.CreateVersion4(), change.ActorMemberId, change.ActorDisplay, change.ChangedAt, "ReviseControlDraft");

    public Task<ControlDraftDiscarded> FreezeAsync(ControlDraftDiscardedContent change) =>
        FreezeAsync<ControlDraftDiscarded>(change, change.TenantId, change.ProgramId, change.ControlId,
            Uuid.CreateVersion4(), change.ActorMemberId, change.ActorDisplay, change.DiscardedAt, "DiscardControlDraft");

    public Task<ControlReviewed> FreezeAsync(ControlReviewedContent change) =>
        FreezeAsync<ControlReviewed>(change, change.TenantId, change.ProgramId, change.ControlId,
            change.DecisionId, change.ActorMemberId, change.ActorDisplay, change.DecidedAt, "ReviewControl");

    public Task<ControlApproved> FreezeAsync(ControlApprovedContent change) =>
        FreezeAsync<ControlApproved>(change, change.TenantId, change.ProgramId, change.ControlId,
            change.ApprovalDecisionId, change.ActorMemberId, change.ActorDisplay, change.DecidedAt, "ApproveControl");

    public Task<ControlSuccessorProposed> FreezeAsync(ControlSuccessorProposedContent change) =>
        FreezeAsync<ControlSuccessorProposed>(change, change.TenantId, change.ProgramId, change.ControlId,
            change.VersionId, change.ActorMemberId, change.ActorDisplay, change.ChangedAt, "ProposeControlSuccessor");

    public Task<ControlRetirementProposed> FreezeAsync(ControlRetirementProposedContent change) =>
        FreezeAsync<ControlRetirementProposed>(change, change.TenantId, change.ProgramId, change.ControlId,
            change.RetirementId, change.ActorMemberId, change.ActorDisplay, change.ChangedAt, "ProposeControlRetirement");

    public Task<ControlRetired> FreezeAsync(ControlRetiredContent change) =>
        FreezeAsync<ControlRetired>(change, change.TenantId, change.ProgramId, change.ControlId,
            change.DecisionId, change.ActorMemberId, change.ActorDisplay, change.DecidedAt, "RetireControl");

    public Task<ControlProposalWithdrawn> FreezeAsync(ControlProposalWithdrawnContent change) =>
        FreezeAsync<ControlProposalWithdrawn>(change, change.TenantId, change.ProgramId, change.ControlId,
            change.DecisionId, change.ActorMemberId, change.Actor.Display, change.WithdrawnAt, "WithdrawControlProposal");

    async Task<TFact> FreezeAsync<TFact>(IControlMutationContent change, Uuid tenantId, Uuid programId,
        Uuid controlId, Uuid identity, Uuid actorId, string display, DateTimeOffset at, string commandKind)
        where TFact : DomainEvent
    {
        var key = (tenantId, controlId);
        if (!_rules.TryGetValue(key, out var rules))
        {
            rules = new(tenantId, controlId);
            _rules.Add(key, rules);
            if (change is not ControlDraftCreatedContent)
            {
                // Isolated forged approval/revision fixtures need a complete immutable envelope,
                // without changing the supplied event fields whose rejection the projection tests.
                var content = change switch
                {
                    ControlDraftRevisedContent revised => revised.Content,
                    ControlApprovedContent approved => approved.Content,
                    _ => new ControlDraftContent("Control", "Objective", "Description", "Narrative", ["Record"]),
                };
                rules.Apply(new ControlDraftCreatedContent(tenantId, programId, controlId,
                    Uuid.CreateVersion4(), "AC-01", content, actorId, display, at));
            }
        }
        var changes = new List<IControlMutationContent>();
        if (change is ControlApprovedContent approval)
        {
            // The isolated version-index fixture supplies the approved body directly.
            // Freeze that exact body; do not replace it with the fixture's earlier draft.
            var approvedDraft = new ControlDraftRevisedContent(tenantId, programId, controlId,
                approval.Revision, approval.Content, actorId, display, at);
            rules.Apply(approvedDraft);
            changes.Add(approvedDraft);
        }
        rules.Apply(change);
        changes.Add(change);
        if (change is ControlSuccessorProposedContent successor)
        {
            var revised = new ControlDraftRevisedContent(tenantId, programId, controlId, successor.Revision,
                rules.CurrentContent!, actorId, display, at);
            rules.Apply(revised);
            changes.Add(revised);
        }
        else if (change is ControlProposalWithdrawnContent withdrawn)
        {
            var revised = new ControlDraftRevisedContent(tenantId, programId, controlId, withdrawn.Revision + 1,
                rules.ApprovedVersion!.Content, actorId, display, at);
            rules.Apply(revised);
            changes.Add(revised);
        }
        var owner = new ControlDraft(tenantId, controlId);
        var operation = OwnerProvenance.For(owner, identity, commandKind,
            ActorReference.ForMember(actorId, display), "projection-fixture", null,
            AuthoredIntentDigest.Compute(change), at);
        var sequence = _sequences.GetValueOrDefault(key) + 1;
        _sequences[key] = sequence;
        return (TFact)await ControlSeeds.FreezeAsync(Content, rules, changes, operation, sequence,
            CancellationToken.None);
    }
}
