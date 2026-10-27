using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A signed revocation assertion ended the designation. The complete
///     <see cref="FirmProfessionalDutyDesignationView" /> is immutable content; no cumulative catalog is carried.
/// </summary>
[Discriminator("bdgrz.firm.professional-duty.designation-revoked", 1)]
public sealed record FirmProfessionalDutyDesignationRevoked(Uuid OperationId, ImmutableContentRef DesignationContent,
    string SignedAssertion, OperationProvenance Operation, ulong Revision, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
