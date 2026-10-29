using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Boundaries.Events;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Responsibilities.Events;
namespace Bdgrz.Compliance.Features.Boundaries.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$boundary_change")]
[JsonDerivedType(typeof(BoundaryDraftCreatedContent), "BoundaryDraftCreated")]
[JsonDerivedType(typeof(BoundaryDraftRevisedContent), "BoundaryDraftRevised")]
[JsonDerivedType(typeof(BoundaryReviewedContent), "BoundaryReviewed")]
[JsonDerivedType(typeof(BoundaryApprovedContent), "BoundaryApproved")]
[JsonDerivedType(typeof(BoundaryDraftDiscardedContent), "BoundaryDraftDiscarded")]
[JsonDerivedType(typeof(BoundarySuccessorProposedContent), "BoundarySuccessorProposed")]
[JsonDerivedType(typeof(ResponsibilityAssigned), "ResponsibilityAssigned")]
[JsonDerivedType(typeof(ResponsibilityRevoked), "ResponsibilityRevoked")]
public interface IBoundaryMutationContent { }
