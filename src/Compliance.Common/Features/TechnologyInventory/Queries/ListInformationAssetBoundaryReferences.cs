using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Contracts;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Queries;

[Discriminator("bdgrz.inventory.asset.boundary_references.list", 1)]
/// <summary>Lists current draft and current or historical approved boundary references, after source catchup.</summary>
public sealed record ListInformationAssetBoundaryReferences(Uuid TenantId, Uuid InformationAssetId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationBoundaryReferenceView>>, ITechnologyInventoryReadRequest, ICallable;
