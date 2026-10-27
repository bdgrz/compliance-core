using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A denial-only receipt that one accepted staff member's directory record changed after the acceptance. The
///     complete original <see cref="DirectoryIndependenceReevaluationView" /> is immutable content; the receipt never
///     reactivates an old acceptance. <paramref name="Revision" /> is the engagement revision after it.
/// </summary>
[Discriminator("bdgrz.independence.directory-reevaluated", 1)]
public sealed record DirectoryIndependenceReevaluated(Uuid TenantId, ImmutableContentRef ReevaluationContent,
    OperationProvenance Operation, ulong Revision, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
