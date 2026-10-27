using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Models;
namespace Bdgrz.Compliance.Features.Providers.Events;

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
