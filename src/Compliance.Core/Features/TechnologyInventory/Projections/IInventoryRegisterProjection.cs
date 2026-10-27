using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Projections;

public interface IInventoryRegisterProjection : IProjectionStore
{
    /// <summary>Projects an applied location revision with the complete content it references.</summary>
    ValueTask ApplyAsync(LocationRevisionRecorded ev, LocationContent content, CancellationToken ct = default);

    /// <summary>Projects an applied operational process revision with the complete content it references.</summary>
    ValueTask ApplyAsync(OperationalProcessRevisionRecorded ev, OperationalProcessContent content,
        CancellationToken ct = default);
}
