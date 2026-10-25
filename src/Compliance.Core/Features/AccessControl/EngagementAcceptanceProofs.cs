using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Builds the grant manifests the authorize and complete receivers carry. Each manifest lists the exact native
///     facts the owners hold, in the canonical stream key order of the frozen manifests, and is read from the owners
///     rather than from the process record, so a crash between receivers never loses what an earlier one produced. The
///     content is keyed by the root admission, so every step that needs it writes the identical bytes to the identical
///     id.
/// </summary>
static class EngagementAcceptanceProofs
{
    /// <summary>The exact grants that hold every required source use and the service epoch.</summary>
    public static Task<ImmutableContentRef> SourceUsesAsync(IAggregateReader reader, IImmutableContentStore content,
        NativeFactReference root, EngagementIndependenceIntent intent, Uuid operationId,
        IReadOnlyList<SourceUseRequirement> requirements, CancellationToken ct) =>
        ManifestAsync(content, root, intent, "engagement-independence-source-use-proofs",
            async () =>
            {
                var tenantId = EngagementIndependenceIntent.TenantOf(intent.Header);
                var proofs = new List<NativeFactReference>(requirements.Count);
                foreach (var requirement in requirements)
                {
                    var key = EngagementAcceptanceEffects.UseKey(operationId, root.Stream, requirement.SourceOwner);
                    var owner = await EngagementUseOwners.HydrateAsync(reader, requirement.SourceOwner, intent.Header,
                        tenantId, key, ct).ConfigureAwait(false) ?? throw new SourceFactVerificationException(
                        "A required source use names an unknown owner.");
                    proofs.Add(owner.Uses.GrantOf(key)?.GrantEvent ?? throw new SourceFactVerificationException(
                        $"{requirement.SourceOwner.Area} has not produced its grant yet."));
                }

                return proofs;
            }, ct);

    /// <summary>The exact grants that reserve every practice subject.</summary>
    public static Task<ImmutableContentRef> ReservationsAsync(IAggregateReader reader, IImmutableContentStore content,
        NativeFactReference root, EngagementIndependenceIntent intent, Uuid operationId,
        IReadOnlyList<PracticeSubjectIntent> subjects, CancellationToken ct) =>
        ManifestAsync(content, root, intent, "engagement-independence-reservation-proofs",
            async () =>
            {
                var tenantId = EngagementIndependenceIntent.TenantOf(intent.Header);
                var proofs = new List<NativeFactReference>(subjects.Count);
                foreach (var subject in subjects)
                {
                    var owner = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId, subject.Kind,
                        subject.SubjectId).ForSourceUse(candidate => EngagementAcceptanceEffects.UseKey(operationId, root.Stream, candidate.Stream)), ct).ConfigureAwait(false);
                    var key = EngagementAcceptanceEffects.UseKey(operationId, root.Stream, owner.Stream);
                    proofs.Add(owner.Uses.GrantOf(key)?.GrantEvent ?? throw new SourceFactVerificationException(
                        $"The reservation of {subject.Key} has not been produced yet."));
                }

                return proofs;
            }, ct);

    /// <summary>The exact permanent commitment of every practice subject.</summary>
    public static Task<ImmutableContentRef> CommitmentsAsync(IAggregateReader reader, IImmutableContentStore content,
        NativeFactReference root, EngagementIndependenceIntent intent, Uuid operationId,
        IReadOnlyList<PracticeSubjectIntent> subjects, CancellationToken ct) =>
        ManifestAsync(content, root, intent, "engagement-independence-commitment-proofs",
            async () =>
            {
                var tenantId = EngagementIndependenceIntent.TenantOf(intent.Header);
                var proofs = new List<NativeFactReference>(subjects.Count);
                foreach (var subject in subjects)
                {
                    var owner = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId, subject.Kind,
                        subject.SubjectId), ct).ConfigureAwait(false);
                    proofs.Add(owner.CommitmentOf(operationId) is { } committed
                        ? NativeFactReference.Of(owner.Stream, committed)
                        : throw new SourceFactVerificationException(
                            $"The commitment of {subject.Key} has not been produced yet."));
                }

                return proofs;
            }, ct);

    static async Task<ImmutableContentRef> ManifestAsync(IImmutableContentStore content, NativeFactReference root,
        EngagementIndependenceIntent intent, string schema, Func<Task<List<NativeFactReference>>> build,
        CancellationToken ct)
    {
        var proofs = await build().ConfigureAwait(false);
        return await content.WriteJsonAsync(IndependenceContent.Realm(EngagementIndependenceIntent.TenantOf(intent.Header)),
            Uuid.CreateVersion5(root.EventId, schema), EngagementIndependenceIntent.ProofSchema, proofs.ToArray(), ct)
            .ConfigureAwait(false);
    }
}
