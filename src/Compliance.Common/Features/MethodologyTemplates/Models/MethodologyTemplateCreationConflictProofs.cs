using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Exact admitted name claim and the committed immutable line creation that refused its intent.</summary>
public sealed record MethodologyTemplateCreationConflictProofs(SourceFactProof<MethodologyTemplateNameClaimed> Claim,
    SourceFactProof<MethodologyTemplateCreatedV4> ExistingCreation);
