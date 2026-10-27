using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the record artifact disclosure delivery intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.record-artifact-disclosure-delivery", 1)]
public sealed record RecordArtifactDisclosureDelivery(Uuid TenantId, Uuid DisclosureId, Uuid ArtifactId,
    long ExpectedRevision, DateTimeOffset DeliveredAt, string DeliveryReference) : IRequest,
    IArtifactDisclosureRequest, ICallable;
