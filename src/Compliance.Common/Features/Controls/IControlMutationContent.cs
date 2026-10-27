using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Responsibilities;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Closed business change payloads applied within one native Control owner acceptance.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$control_change")]
[JsonDerivedType(typeof(ControlDraftCreatedContent), "ControlDraftCreated")]
[JsonDerivedType(typeof(ControlDraftRevisedContent), "ControlDraftRevised")]
[JsonDerivedType(typeof(ControlDraftDiscardedContent), "ControlDraftDiscarded")]
[JsonDerivedType(typeof(ControlReviewedContent), "ControlReviewed")]
[JsonDerivedType(typeof(ControlApprovedContent), "ControlApproved")]
[JsonDerivedType(typeof(ControlSuccessorProposedContent), "ControlSuccessorProposed")]
[JsonDerivedType(typeof(ControlRetirementProposedContent), "ControlRetirementProposed")]
[JsonDerivedType(typeof(ControlRetiredContent), "ControlRetired")]
[JsonDerivedType(typeof(ControlProposalWithdrawnContent), "ControlProposalWithdrawn")]
[JsonDerivedType(typeof(ControlOwnerPersonDesignatedContent), "ControlOwnerPersonDesignated")]
[JsonDerivedType(typeof(ResponsibilityAssigned), "ResponsibilityAssigned")]
[JsonDerivedType(typeof(ResponsibilityRevoked), "ResponsibilityRevoked")]
public interface IControlMutationContent { }
