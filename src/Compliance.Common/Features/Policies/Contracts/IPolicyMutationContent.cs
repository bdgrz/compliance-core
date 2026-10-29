using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Policies.Models;
namespace Bdgrz.Compliance.Features.Policies.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$policy_change")]
[JsonDerivedType(typeof(PolicyDraftCreatedContent), "PolicyDraftCreated")]
[JsonDerivedType(typeof(PolicyDraftRevisedContent), "PolicyDraftRevised")]
[JsonDerivedType(typeof(PolicyReviewedContent), "PolicyReviewed")]
[JsonDerivedType(typeof(PolicyApprovedContent), "PolicyApproved")]
[JsonDerivedType(typeof(PolicyPeriodicReviewConfirmedContent), "PolicyPeriodicReviewConfirmed")]
[JsonDerivedType(typeof(PolicyRetirementProposedContent), "PolicyRetirementProposed")]
[JsonDerivedType(typeof(PolicyRetiredContent), "PolicyRetired")]
[JsonDerivedType(typeof(PolicyDraftDiscardedContent), "PolicyDraftDiscarded")]
public interface IPolicyMutationContent { }
