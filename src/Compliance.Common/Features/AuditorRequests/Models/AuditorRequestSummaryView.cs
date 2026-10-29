using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>Engagement-scoped requests and their current work status.</summary>
public sealed record AuditorRequestSummaryView(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId, string Name, string Status, DateTimeOffset UpdatedAt);
