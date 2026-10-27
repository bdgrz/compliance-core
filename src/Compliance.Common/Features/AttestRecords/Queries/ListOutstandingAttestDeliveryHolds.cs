using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AttestRecords.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.attest-records.list-outstanding-attest-delivery-holds", 1)]
public sealed record ListOutstandingAttestDeliveryHolds(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AttestDeliveryHoldView>>, IProgramReadRequest, ICallable;
