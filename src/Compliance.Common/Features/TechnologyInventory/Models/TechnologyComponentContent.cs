using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Models;

/// <summary>
///     One governed component at account and environment granularity (M0-D08). A
///     <c>cloud_account</c> component shares its identity with its system instance.
/// </summary>
public sealed record TechnologyComponentContent(string Category, string Name,
    Uuid OwnerPersonId, string? EnvironmentReference, string? LocationReference,
    Uuid? SystemInstanceId, int? EndpointCount, string? ManagementSource, string Lifecycle);
