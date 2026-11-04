using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>
///     One governed environment, network, data store, repository or endpoint class.
///     An optional system instance reference names the cloud account containing it.
/// </summary>
public sealed record TechnologyComponentContent(string Category, string Name,
    Uuid OwnerPersonId, string? EnvironmentReference, string? LocationReference,
    Uuid? SystemInstanceId, int? EndpointCount, string? ManagementSource, string Lifecycle);
