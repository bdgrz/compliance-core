using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Services;

/// <summary>
///     The one canonical identity of a control occurrence. It derives solely from the tenant, program, control and
///     canonical business window, so every generator and every plan version that names the same window names the same
///     occurrence.
/// </summary>
public static class ControlOccurrenceIdentity
{
    /// <summary>The identity owner type of an occurrence.</summary>
    public const string OwnerType = "ControlOccurrence";

    /// <summary>The identity owner type of a control's operating plan line.</summary>
    public const string PlanOwnerType = "ControlOperatingPlan";

    /// <summary>The occurrence id of a business window.</summary>
    public static Uuid Of(Uuid tenantId, Uuid programId, Uuid controlId, ControlBusinessWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return AggregateIdentity.Create(OwnerType, tenantId, programId, controlId, window.CanonicalKey());
    }

    /// <summary>The calendar window of a recurring period; the end of the period is inclusive in the period model.</summary>
    public static ControlCalendarWindow WindowOf(ExpectedPeriod period) =>
        new(period.Start, period.End.AddDays(1));

    /// <summary>The identity of a control's operating plan line.</summary>
    public static Uuid PlanLineOf(Uuid tenantId, Uuid programId, Uuid controlId) =>
        AggregateIdentity.Create(PlanOwnerType, tenantId, programId, controlId);
}
