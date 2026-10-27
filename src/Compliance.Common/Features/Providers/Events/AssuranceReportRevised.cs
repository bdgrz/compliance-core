using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     An assurance report was revised at the expected revision of its own owner; the provider cannot change. The
///     complete revised <see cref="AssuranceReportContent" /> is immutable content and
///     <paramref name="RequestId" /> is the command id of the revision.
/// </summary>
[Discriminator("bdgrz.provider.assurance_report.revised", 1)]
public sealed record AssuranceReportRevised(Uuid TenantId, Uuid ReportId, Uuid ProviderId, Uuid RequestId,
    long Revision, ImmutableContentRef ContentContent, long ProviderRevision, ActorReference Actor,
    DateTimeOffset RecordedAt, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
