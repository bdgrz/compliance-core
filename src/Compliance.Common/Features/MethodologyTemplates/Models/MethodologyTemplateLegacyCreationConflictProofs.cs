using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>A name admission lost to an already committed legacy identity, which can never be recreated as a new line.</summary>
public sealed record MethodologyTemplateLegacyCreationConflictProofs(SourceFactProof<MethodologyTemplateNameClaimed> Claim,
    SourceFactProof<MethodologyTemplateCreated> ExistingCreation);
