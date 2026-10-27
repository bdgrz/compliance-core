using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

public interface IInventoryRegisterProjection : IProjectionStore
{
    /// <summary>Projects an applied location revision with the complete content it references.</summary>
    ValueTask ApplyAsync(LocationRevisionRecorded ev, LocationContent content, CancellationToken ct = default);

    /// <summary>Projects an applied operational process revision with the complete content it references.</summary>
    ValueTask ApplyAsync(OperationalProcessRevisionRecorded ev, OperationalProcessContent content,
        CancellationToken ct = default);
}
