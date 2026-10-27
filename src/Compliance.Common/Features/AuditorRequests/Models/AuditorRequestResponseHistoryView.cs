using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The complete response draft, review, delivery, and disposition history for a request.</summary>
public sealed record AuditorRequestResponseHistoryView(
    Uuid ResponseId,
    int CurrentVersion,
    int? DeliveredVersion,
    string Status,
    IReadOnlyList<AuditorResponseVersionView> Versions,
    AuditorResponseDeliveryView? Delivery,
    AuditorResponseDispositionView? Disposition);
