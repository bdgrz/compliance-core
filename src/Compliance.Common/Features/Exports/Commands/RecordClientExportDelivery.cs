using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
namespace Bdgrz.Compliance.Features.Exports.Commands;

/// <summary>Records the record client export delivery intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.exports.record-client-export-delivery", 2)]
public sealed record RecordClientExportDelivery(Uuid TenantId, Uuid ExportId, long ExpectedRevision,
    string Recipient, DateTimeOffset DeliveredAt, string DeliveryReference)
    : IRequest, IClientManagementMutationRequest, ICallable;
