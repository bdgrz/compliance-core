using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Responsibilities;

namespace Bdgrz.Compliance.Features.Commitments;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$commitment_change")]
[JsonDerivedType(typeof(CommitmentDraftCreatedContent), "CommitmentDraftCreated")]
[JsonDerivedType(typeof(CommitmentDraftRevisedContent), "CommitmentDraftRevised")]
[JsonDerivedType(typeof(CommitmentReviewedContent), "CommitmentReviewed")]
[JsonDerivedType(typeof(CommitmentApprovedContent), "CommitmentApproved")]
[JsonDerivedType(typeof(CommitmentRetiredContent), "CommitmentRetired")]
[JsonDerivedType(typeof(ResponsibilityAssigned), "ResponsibilityAssigned")]
[JsonDerivedType(typeof(ResponsibilityRevoked), "ResponsibilityRevoked")]
public interface ICommitmentMutationContent { }
