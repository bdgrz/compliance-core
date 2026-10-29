using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
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
