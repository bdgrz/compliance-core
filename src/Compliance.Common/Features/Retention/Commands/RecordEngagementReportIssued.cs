using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
namespace Bdgrz.Compliance.Features.Retention.Commands;

/// <summary>Records the record engagement report issued intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.record-engagement-report-issued", 1)]
public sealed record RecordEngagementReportIssued(Uuid TenantId, Uuid HoldId, Uuid EngagementId, Uuid ReportArtifactId, DateTimeOffset IssuedAt, string IssuerReference) : IRequest, IClientManagementMutationRequest, ICallable;
