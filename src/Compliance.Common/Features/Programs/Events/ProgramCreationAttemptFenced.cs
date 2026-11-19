using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>Fences earlier uncommitted creation attempts without creating the program.</summary>
[Discriminator("bdgrz.program.creation_attempt.fenced", 1)]
public sealed record ProgramCreationAttemptFenced(Uuid TenantId, Uuid ProgramId, ulong Generation,
    ulong ExpectedPosition, OperationProvenance Operation, AccessAdmissionEvidence AccessAdmission) : DomainEvent;
