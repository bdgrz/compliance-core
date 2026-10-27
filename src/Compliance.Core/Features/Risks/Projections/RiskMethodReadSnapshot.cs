using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Projections;

/// <summary>Verified read composition; publication history is never command-owner state.</summary>
public sealed record RiskMethodReadSnapshot(IReadOnlyList<RiskMethodVersionView> Versions,
    IReadOnlyList<RiskMethodVersionPublished> Facts)
{
    public long PublishedVersion => Versions.Count;
    public RiskMethodVersionView? Current => Versions.Count == 0 ? null : Versions[^1];
    public RiskMethodVersionView? GetVersion(long version) => version > 0 && version <= Versions.Count ? Versions[(int)version - 1] : null;
    public RiskMethodVersionView? Find(Cntryl.Portia.Uuid id) => Versions.FirstOrDefault(version => version.MethodVersionId == id);
}
