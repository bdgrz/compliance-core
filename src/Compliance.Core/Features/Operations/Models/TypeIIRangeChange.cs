using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>A verified accepted opening or amendment with the period owner it was found on, or why it is not verified.</summary>
sealed record TypeIIRangeChange(Uuid TenantId, TypeIIPeriod? Period, DomainEvent? Accepted, long AcceptedRevision,
    TypeIIRangeIntent? Intent, RequestError? Error)
{
    public static TypeIIRangeChange Failed(RequestError error) => new(Uuid.Empty, null, null, 0, null, error);
}
