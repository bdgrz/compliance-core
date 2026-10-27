using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Contracts;

/// <summary>A governed inventory mutation, requiring both ordinary grants and the client management independence wall.</summary>
public interface ITechnologyInventoryWriteRequest : ITechnologyInventoryRequest, IClientManagementMutationRequest;
