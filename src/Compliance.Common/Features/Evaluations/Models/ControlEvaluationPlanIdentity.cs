using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations;

/// <summary>Identity of one immutable publication on a scoped plan line at its original expected version.</summary>
public static class ControlEvaluationPlanIdentity
{
    public static Uuid For(Uuid tenant, Uuid program, Uuid control, long expectedVersion) =>
        Uuid.CreateVersion5(control, $"control-evaluation-plan-definition:{tenant}:{program}:" +
            expectedVersion.ToString(CultureInfo.InvariantCulture));
}
