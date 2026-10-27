using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Events;

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
