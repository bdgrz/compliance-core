using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

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
