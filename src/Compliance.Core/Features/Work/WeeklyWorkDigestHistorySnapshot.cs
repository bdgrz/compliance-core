using Bdgrz.Compliance.Features.Work.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>The complete verified weekly read history, separate from its bounded command owner.</summary>
public sealed record WeeklyWorkDigestHistorySnapshot(IReadOnlyList<DomainEvent> Facts, WeeklyWorkDigestView View);
