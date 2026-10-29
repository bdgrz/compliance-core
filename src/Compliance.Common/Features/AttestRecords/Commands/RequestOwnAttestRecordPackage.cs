using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Models;
namespace Bdgrz.Compliance.Features.AttestRecords.Commands;

/// <summary>Records the request own attest record package intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.attest-records.request-own-attest-record-package", 1)]
public sealed record RequestOwnAttestRecordPackage(Uuid TenantId, Uuid EngagementId, Uuid PackageId, string PackageKey) : IRequest, IClientManagementMutationRequest, ICallable;
