using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Stable logical identities retain the accepted build and personal-handoff algorithms.</summary>
public static class AuditPackageCommandIdentity
{
    static readonly Uuid BuildNamespace = Uuid.Parse("698c0bb6-2dfc-58b3-b2fd-2f3dc04369f8", CultureInfo.InvariantCulture);
    static readonly Uuid DeliveryNamespace = Uuid.Parse("53cb20d6-01ef-5fd5-99b3-04087e39bf77", CultureInfo.InvariantCulture);
    public static Uuid Build(Uuid packageId, long version, string key) => Uuid.CreateVersion5(BuildNamespace,
        $"{packageId}\n{version}\n{(key?.Trim() ?? string.Empty)}");
    public static Uuid Delivery(Uuid packageId, long version, string recipient, string reference) =>
        Uuid.CreateVersion5(DeliveryNamespace, $"{packageId}\n{version}\n{(recipient?.Trim() ?? string.Empty)}\n{(reference?.Trim() ?? string.Empty)}");
}
