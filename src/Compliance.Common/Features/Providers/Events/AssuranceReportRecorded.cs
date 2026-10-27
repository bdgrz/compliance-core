using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     An assurance report was recorded as the first revision of its own owner. The complete
///     <see cref="AssuranceReportContent" /> is immutable content; <paramref name="RequestId" /> is the report id,
///     the logical identity of the creation.
/// </summary>
[Discriminator("bdgrz.provider.assurance_report.recorded", 1)]
public sealed record AssuranceReportRecorded(Uuid TenantId, Uuid ReportId, Uuid ProviderId, Uuid RequestId,
    ImmutableContentRef ContentContent, long ProviderRevision, ActorReference Actor, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
