using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Projections;

/// <summary>The complete verified weekly read history, separate from its bounded command owner.</summary>
public sealed record WeeklyWorkDigestHistorySnapshot(IReadOnlyList<DomainEvent> Facts, WeeklyWorkDigestView View);
