using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

/// <summary>Test-only root native fact of the acceptance probe.</summary>
[Discriminator("test.probe.admitted", 1)]
sealed record ProbeAdmitted(Uuid ProbeId, string Payload, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
