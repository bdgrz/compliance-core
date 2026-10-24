using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
///     Locations and operational processes each live in their own owner stream inside one tenant area, so one
///     projection serves both. Active-name uniqueness is decided on separate name-claim owners, never inside a shared
///     register stream.
/// </summary>
static class InventoryRegisters
{
    public const string Area = "inventory-registers";
    public const string ManualSource = "manual";

    public static EventStreamAddress LocationAddress(Uuid tenantId, Uuid locationId) =>
        new(tenantId.ToString(), Area, $"location-{locationId}");

    public static EventStreamAddress OperationalProcessAddress(Uuid tenantId, Uuid processId) =>
        new(tenantId.ToString(), Area, $"operational-process-{processId}");

    public static EventStreamPattern TenantPattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), Area);
}
