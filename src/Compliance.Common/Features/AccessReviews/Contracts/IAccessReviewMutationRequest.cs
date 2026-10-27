using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Contracts;

/// <summary>A client management record mutation, including an assigned reviewer's access decision.</summary>
public interface IAccessReviewMutationRequest : IAccessReviewRequest, IClientManagementMutationRequest;
