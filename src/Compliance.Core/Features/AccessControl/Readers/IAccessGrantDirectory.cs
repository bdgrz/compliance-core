using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

public interface IAccessGrantDirectory
{
    ValueTask<AccessGrantSetView> ListAsync(Uuid tenantId, CancellationToken ct = default);

    ValueTask<AccessGrantView?> GetAsync(Uuid tenantId, Uuid grantId, CancellationToken ct = default);

    /// <summary>Returns the member episode bound to a direct grant, or null for legacy or team grants.</summary>
    ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid grantId,
        CancellationToken ct = default) => ValueTask.FromResult<Uuid?>(null);

    ValueTask<IReadOnlySet<Uuid>> FindPendingRevocationsAsync(Uuid tenantId, IReadOnlySet<Uuid> grantIds,
        CancellationToken ct = default);
}

public sealed record ProgramAccessVisibility(bool OrganizationWide, IReadOnlySet<Uuid> ProgramIds)
{
    public bool HasAnyAccess => OrganizationWide || ProgramIds.Count > 0;
}

public interface IAccessGrantProjection : IProjectionStore
{
    ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
