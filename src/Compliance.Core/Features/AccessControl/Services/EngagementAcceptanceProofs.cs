using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

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
                    var key = EngagementAcceptanceEffects.UseKey(operationId, root.Stream, subject.Stream);
                    var owner = await reader.HydrateAsync(new PracticeIndependenceCommitment(tenantId, subject.Kind,
                        subject.SubjectId, selectedReservation: key), ct).ConfigureAwait(false);
                    proofs.Add(owner.CommitmentOf(key) is { } committed
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
