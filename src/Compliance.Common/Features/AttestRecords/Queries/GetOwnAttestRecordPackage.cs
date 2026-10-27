using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AttestRecords.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AttestRecords.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.attest-records.get-own-attest-record-package", 1)]
public sealed record GetOwnAttestRecordPackage(Uuid TenantId, Uuid ProgramId, Uuid OwnAttestRecordPackageId)
    : IRequest<OwnAttestRecordPackageView>, IProgramReadRequest, ICallable;
