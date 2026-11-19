using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Resolves the committed name admission and original operator intent shared by creation and refusal.</summary>
static class MethodologyTemplateCreationSources
{
    public static bool IsClaimReference(NativeFactReference? source) => source is { Stream: { Realm: MethodologyTemplate.Realm, Area: MethodologyTemplateNameClaim.Area } } &&
        source.EventId != Uuid.Empty && source.AggregateVersion > 0;

    public static async Task<Admission?> ResolveAsync(ISourceFactResolver sources, IImmutableContentStore content,
        Uuid templateId, NativeFactReference source, ulong position, CancellationToken ct)
    {
        var (proof, claim, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateNameClaimed>(sources, content,
            MethodologyTemplate.Realm, source, position, ct).ConfigureAwait(false);
        var intent = await content.ReadJsonAsync<MethodologyTemplateCreationIntent>(MethodologyTemplate.Realm,
            claim.Intent, ct).ConfigureAwait(false);
        if (!MethodologyTemplateNameClaim.ValidIntent(intent) || intent.TemplateId != templateId ||
            source.Stream != MethodologyTemplateNameClaim.AddressOf(intent.Name) ||
            !new MethodologyTemplateNameClaim(intent.Name).MatchesAdmission(claim, intent))
            return null;
        return new Admission(proof, claim, intent);
    }

    public static Uuid RefusalId(Uuid claimId, Uuid existingId) => Uuid.CreateVersion5(claimId,
        $"{nameof(RefuseMethodologyTemplateCreation)}:{existingId}");
    public static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Template creation requires its exact committed name admission and immutable original operator intent."));

    internal sealed record Admission(SourceFactProof<MethodologyTemplateNameClaimed> Proof,
        MethodologyTemplateNameClaimed Claim, MethodologyTemplateCreationIntent Intent);
}
