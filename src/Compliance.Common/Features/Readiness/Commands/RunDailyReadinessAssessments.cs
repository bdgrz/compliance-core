using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Commands;

/// <summary>Trusted daily trigger; discovery is advisory and each program is checked at source.</summary>
[Discriminator("bdgrz.readiness.daily.run", 1)]
[RequestRoute("*", "readiness", "daily", "run")]
public sealed record RunDailyReadinessAssessments(Uuid TenantId) : IRequest, ISchedulable;
