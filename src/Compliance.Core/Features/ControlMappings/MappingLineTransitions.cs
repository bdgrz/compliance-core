using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

static class MappingLineTransitions
{
    public static MappingLineHeader Next(Aggregate owner, MappingLineHeader before, DomainEvent fact)
    {
        if (fact is not IMappingLineFact native || native.Header is null || native.Content is null || native.Operation is null ||
            native.Operation.AuthoredIntentSha256 is not { Length: 64 } ||
            native.FirstResponse is null || native.OperationResult is null || native.Revision != before.Revision + 1 ||
            native.Content.Schema != MappingLineContent.Schema ||
            native.Content.MediaType != "application/json" || native.Content.Bytes <= 0 ||
            native.Content.ContentId == Uuid.Empty || native.Content.Sha256 is not { Length: 64 } || !native.Content.Sha256.All(Uri.IsHexDigit))
            throw new ImmutableContentIntegrityException("A line fact lacks its complete native revision/content binding.");
        MappingLineHeader next;
        switch (fact)
        {
            case ControlCriterionMappingProposed ev:
                if (owner is not ControlCriterionMapping || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) || ev.ControlId == Uuid.Empty || owner.Id != ControlCriterionMapping.IdentityOf(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EditionId, ev.CriterionIdentifier) ||
                    owner.Stream.Realm != ev.TenantId.ToString() || ev.TenantId == Uuid.Empty || ev.ProgramId == Uuid.Empty ||
                    ev.EditionId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) ||
                    !NativeOwnerHistory.IsOwnRoot(owner, ev.Operation, nameof(ProposeControlCriterionMapping), (ulong)before.Revision) ||
                    ev.Operation.Actor != ev.Actor || ev.Operation.AcceptedAt != ev.ProposedAt || ev.ActorMemberId == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A line fact has another tuple, actor or native root binding.");
                if (ev.ControlVersionId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.CriterionKind) ||
                    before.Pending is not null || ev.VersionNumber != before.VersionCount + 1 ||
                    ev.Operation.LogicalId != ev.MappingId || ev.MappingId == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A proposal does not follow its resolved line.");
                next = before with { Revision = ev.Revision, VersionCount = ev.VersionNumber, FirstProposalId = before.VersionCount == 0 ? ev.MappingId : before.FirstProposalId, Pending = new MappingLineSlot(ev.MappingId, ev.VersionNumber, ev.ActorMemberId) };
                break;
            case ControlCriterionMappingReviewed ev:
                if (owner is not ControlCriterionMapping || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) || ev.ControlId == Uuid.Empty || owner.Id != ControlCriterionMapping.IdentityOf(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EditionId, ev.CriterionIdentifier) ||
                    owner.Stream.Realm != ev.TenantId.ToString() || ev.TenantId == Uuid.Empty || ev.ProgramId == Uuid.Empty ||
                    ev.EditionId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) ||
                    !NativeOwnerHistory.IsOwnRoot(owner, ev.Operation, nameof(ReviewControlCriterionMapping), (ulong)before.Revision) ||
                    ev.Operation.Actor != ev.Actor || ev.Operation.AcceptedAt != ev.DecidedAt || ev.ActorMemberId == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A line fact has another tuple, actor or native root binding.");
                if (before.Pending is not { } pendingControlCriterionMappingReviewed || pendingControlCriterionMappingReviewed.ProposalId != ev.MappingId ||
                    pendingControlCriterionMappingReviewed.VersionNumber != ev.VersionNumber || ev.Operation.LogicalId != ev.DecisionId ||
                    ev.Outcome is not ("accept" or "reject") ||
                    pendingControlCriterionMappingReviewed.ProposerMemberId == ev.ActorMemberId && ev.SeparationOfDutiesWaiverId is null)
                    throw new ImmutableContentIntegrityException("A review does not resolve its exact independent pending proposal.");
                next = before with { Revision = ev.Revision, Pending = null, Active = ev.Outcome == "accept" ? before.Pending : before.Active };
                break;
            case ControlCriterionMappingRetired ev:
                if (owner is not ControlCriterionMapping || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) || ev.ControlId == Uuid.Empty || owner.Id != ControlCriterionMapping.IdentityOf(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EditionId, ev.CriterionIdentifier) ||
                    owner.Stream.Realm != ev.TenantId.ToString() || ev.TenantId == Uuid.Empty || ev.ProgramId == Uuid.Empty ||
                    ev.EditionId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) ||
                    !NativeOwnerHistory.IsOwnRoot(owner, ev.Operation, nameof(RetireControlCriterionMapping), (ulong)before.Revision) ||
                    ev.Operation.Actor != ev.Actor || ev.Operation.AcceptedAt != ev.RetiredAt || ev.ActorMemberId == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A line fact has another tuple, actor or native root binding.");
                if (before.Pending is not null || before.Active is not { } activeControlCriterionMappingRetired ||
                    activeControlCriterionMappingRetired.ProposalId != ev.MappingId || activeControlCriterionMappingRetired.VersionNumber != ev.VersionNumber)
                    throw new ImmutableContentIntegrityException("A termination does not name the exact accepted proposal.");
                next = before with { Revision = ev.Revision, Active = null, EverTerminated = true };
                break;
            case CriterionNotApplicableProposed ev:
                if (owner is not CriterionApplicability || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) || owner.Id != CriterionApplicability.IdentityOf(ev.TenantId, ev.ProgramId, ev.EditionId, ev.CriterionIdentifier) ||
                    owner.Stream.Realm != ev.TenantId.ToString() || ev.TenantId == Uuid.Empty || ev.ProgramId == Uuid.Empty ||
                    ev.EditionId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) ||
                    !NativeOwnerHistory.IsOwnRoot(owner, ev.Operation, nameof(ProposeCriterionNotApplicable), (ulong)before.Revision) ||
                    ev.Operation.Actor != ev.Actor || ev.Operation.AcceptedAt != ev.ProposedAt || ev.ProposerMemberId == Uuid.Empty ||
                    ev.Actor is not { Kind: "member" } || !Uuid.TryParse(ev.Actor.Id, out var proposerMemberId) ||
                    proposerMemberId != ev.ProposerMemberId)
                    throw new ImmutableContentIntegrityException("A line fact has another tuple, actor or native root binding.");
                if (before.Pending is not null || before.Active is not null || ev.VersionNumber != before.VersionCount + 1 ||
                    ev.Operation.LogicalId != ev.DecisionId || ev.DecisionId == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A proposal does not follow its resolved line.");
                next = before with { Revision = ev.Revision, VersionCount = ev.VersionNumber, FirstProposalId = before.VersionCount == 0 ? ev.DecisionId : before.FirstProposalId, Pending = new MappingLineSlot(ev.DecisionId, ev.VersionNumber, ev.ProposerMemberId) };
                break;
            case CriterionApplicabilityReviewed ev:
                if (owner is not CriterionApplicability || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) || owner.Id != CriterionApplicability.IdentityOf(ev.TenantId, ev.ProgramId, ev.EditionId, ev.CriterionIdentifier) ||
                    owner.Stream.Realm != ev.TenantId.ToString() || ev.TenantId == Uuid.Empty || ev.ProgramId == Uuid.Empty ||
                    ev.EditionId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) ||
                    !NativeOwnerHistory.IsOwnRoot(owner, ev.Operation, nameof(ReviewCriterionApplicability), (ulong)before.Revision) ||
                    ev.Operation.Actor != ev.Actor || ev.Operation.AcceptedAt != ev.ReviewedAt || Reviewer(ev.Actor) == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A line fact has another tuple, actor or native root binding.");
                if (before.Pending is not { } pendingCriterionApplicabilityReviewed || pendingCriterionApplicabilityReviewed.ProposalId != ev.DecisionId ||
                    pendingCriterionApplicabilityReviewed.VersionNumber != ev.VersionNumber || ev.Operation.LogicalId != ev.ReviewDecisionId ||
                    ev.Outcome is not ("accept" or "reject") ||
                    pendingCriterionApplicabilityReviewed.ProposerMemberId == Reviewer(ev.Actor) && ev.SeparationOfDutiesWaiverId is null)
                    throw new ImmutableContentIntegrityException("A review does not resolve its exact independent pending proposal.");
                next = before with { Revision = ev.Revision, Pending = null, Active = ev.Outcome == "accept" ? before.Pending : before.Active };
                break;
            case CriterionNotApplicableWithdrawn ev:
                if (owner is not CriterionApplicability || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) || owner.Id != CriterionApplicability.IdentityOf(ev.TenantId, ev.ProgramId, ev.EditionId, ev.CriterionIdentifier) ||
                    owner.Stream.Realm != ev.TenantId.ToString() || ev.TenantId == Uuid.Empty || ev.ProgramId == Uuid.Empty ||
                    ev.EditionId == Uuid.Empty || string.IsNullOrWhiteSpace(ev.CriterionIdentifier) ||
                    !NativeOwnerHistory.IsOwnRoot(owner, ev.Operation, nameof(WithdrawCriterionNotApplicable), (ulong)before.Revision) ||
                    ev.Operation.Actor != ev.Actor || ev.Operation.AcceptedAt != ev.WithdrawnAt || Reviewer(ev.Actor) == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A line fact has another tuple, actor or native root binding.");
                if (before.Pending is not null || before.Active is not { } activeCriterionNotApplicableWithdrawn ||
                    activeCriterionNotApplicableWithdrawn.ProposalId != ev.DecisionId || activeCriterionNotApplicableWithdrawn.VersionNumber != ev.VersionNumber)
                    throw new ImmutableContentIntegrityException("A termination does not name the exact accepted proposal.");
                next = before with { Revision = ev.Revision, Active = null, EverTerminated = true };
                break;
            default:
                throw new ImmutableContentIntegrityException("The line contains another native fact family.");
        }
        if (next != native.Header)
            throw new ImmutableContentIntegrityException("The native line header does not match its complete transition.");
        return next;
    }

    static Uuid Reviewer(ActorReference actor) => actor.Kind == "member" && Uuid.TryParse(actor.Id, out var member)
        ? member : Uuid.Empty;
}
