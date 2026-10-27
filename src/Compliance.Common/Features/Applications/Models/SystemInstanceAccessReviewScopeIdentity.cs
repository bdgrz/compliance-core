using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>Identity of one immutable personal decision on an instance at its original expected decision count.</summary>
public static class SystemInstanceAccessReviewScopeIdentity
{
    public static Uuid For(Uuid tenant, Uuid application, Uuid instance, long expectedCount) =>
        Uuid.CreateVersion5(instance, $"system-instance-access-review-scope-decision:{tenant}:{application}:" +
            expectedCount.ToString(CultureInfo.InvariantCulture));
}
