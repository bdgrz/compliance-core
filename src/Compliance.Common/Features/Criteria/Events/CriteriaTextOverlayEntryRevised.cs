using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Criteria.Events;

/// <summary>
///     One revision of the licensed text overlay of one criterion. The logical id of <paramref name="Operation" />
///     is the command id, and its actor and acceptance time are the attribution of the revision; the complete
///     supplied content is verified immutable content, so the license and text stay exact however large the text is.
/// </summary>
[Discriminator("bdgrz.criteria-overlay.entry-revised", 2)]
public sealed record CriteriaTextOverlayEntryRevised(Uuid TenantId, Uuid EditionId, string Identifier, long Revision,
    ImmutableContentRef OverlayContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
