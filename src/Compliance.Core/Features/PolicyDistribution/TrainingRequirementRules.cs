using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>The content rules and the immutable view of a training requirement version.</summary>
public static class TrainingRequirementRules
{
    /// <summary>The first validation message for the content, or null when it is acceptable.</summary>
    public static string? Validate(TrainingRequirementContent? content)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.CourseName) ||
            content.CourseName.Trim().Length > 200 || content.Description?.Length > 4000)
            return "A training requirement requires a course name of at most 200 characters and a description of at most 4000.";
        if (content.DeliverySource is not ("manual" or "lms_export"))
            return "The delivery source must be manual or lms_export.";
        return PolicyAudience.Validate(content.AudienceKind, content.AudienceTeams);
    }

    /// <summary>The trimmed content the requirement stores.</summary>
    public static TrainingRequirementContent Clean(TrainingRequirementContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new TrainingRequirementContent(content.CourseName.Trim(),
            string.IsNullOrWhiteSpace(content.Description) ? null : content.Description.Trim(),
            content.AudienceKind, PolicyAudience.CleanTeams(content.AudienceTeams), content.DeliverySource);
    }

    /// <summary>The hash of the stored content; it pins a campaign to the exact version it launched.</summary>
    public static string Hash(TrainingRequirementContent content) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content,
            ComplianceCoreJsonContext.Default.TrainingRequirementContent)));

    /// <summary>Reads the exact version of a requirement, or the latest one, from its verified content.</summary>
    public static async ValueTask<TrainingRequirementView?> ViewAsync(IImmutableContentStore store, IDomainEventReader events,
        TrainingRequirement requirement, long? version, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(requirement);
        var facts = await TrainingRequirementHistory.FactsAsync(events, requirement, ct).ConfigureAwait(false);
        var fact = version is { } exact ? facts.FirstOrDefault(value => value.Version == exact) : requirement.Latest;
        return fact is null ? null : await ViewAsync(store, fact, requirement.CurrentVersion, ct).ConfigureAwait(false);
    }

    /// <summary>The view of one defined version, given the requirement's latest version.</summary>
    public static async ValueTask<TrainingRequirementView> ViewAsync(IImmutableContentStore store,
        TrainingRequirementDefined fact, long latestVersion, CancellationToken ct)
    {
        if (fact.ContentContent.Schema != PolicyCampaignContent.TrainingContentSchema ||
            fact.ContentContent.MediaType != ImmutableContentStoreExtensions.JsonMediaType)
            throw new ImmutableContentIntegrityException("The requirement version has another body schema or media type.");
        var content = await store.ReadJsonAsync<TrainingRequirementContent>(PolicyCampaignContent.Realm(fact.TenantId),
            fact.ContentContent, ct).ConfigureAwait(false);
        if (Validate(content) is not null || Hash(content) != fact.ContentSha256 ||
            Hash(Clean(content)) != Hash(content) || fact.Version < 1 || latestVersion < fact.Version ||
            fact.Actor != fact.Operation.Actor || fact.ChangedAt != fact.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The requirement body differs from its exact original version.");
        return new TrainingRequirementView(fact.TenantId, fact.ProgramId, fact.RequirementId, fact.Identifier,
            fact.Version, latestVersion, content, fact.ContentSha256, TrainingRequirement.Cadence, fact.Actor,
            fact.ChangedAt);
    }

    /// <summary>The immutable reference to a stored content's schema, for writers.</summary>
    public static Task<ImmutableContentRef> WriteContentAsync(IImmutableContentStore store, Uuid tenantId,
        TrainingRequirementContent content, CancellationToken ct) =>
        PolicyCampaignContent.WriteAsync(store, tenantId, PolicyCampaignContent.TrainingContentSchema, content, ct);
}
