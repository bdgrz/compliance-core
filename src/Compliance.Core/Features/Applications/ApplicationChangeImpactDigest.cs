using System.Security.Cryptography;
using System.Text.Json;

namespace Bdgrz.Compliance.Features.Applications;

static class ApplicationChangeImpactDigest
{
    public static string Compute(ApplicationChangePreview preview)
    {
        var canonical = preview with
        {
            Changes = preview.Changes.OrderBy(static item => item.Field,
                StringComparer.Ordinal).ToArray(),
            BoundaryReferences = preview.BoundaryReferences
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.BoundaryId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            PendingContexts = preview.PendingContexts.Order(StringComparer.Ordinal).ToArray(),
            ApplicationRelationships = preview.ApplicationRelationships
                .OrderBy(static item => item.SourceApplicationId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.TargetApplicationId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.RelationshipType, StringComparer.Ordinal)
                .ThenBy(static item => item.RelationshipId.ToString(), StringComparer.Ordinal)
                .ToArray(),
            IncomingApplicationRelationships = preview.IncomingApplicationRelationships
                .OrderBy(static item => item.SourceApplicationId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.RelationshipId.ToString(), StringComparer.Ordinal)
                .ToArray(),
            ControlDraftReferences = preview.ControlDraftReferences
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            ApprovedControlReferences = preview.ApprovedControlReferences
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.ControlId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            PolicyReferences = preview.PolicyReferences
                .OrderBy(static item => item.SubjectType, StringComparer.Ordinal)
                .ThenBy(static item => item.GovernedRecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.PolicyId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.Version)
                .ThenBy(static item => item.EntryId.ToString(), StringComparer.Ordinal).ToArray(),
            SystemInstanceReferences = preview.SystemInstanceReferences
                .OrderBy(static item => item.SystemInstanceId.ToString(), StringComparer.Ordinal)
                .ToArray(),
            ImpactDigest = string.Empty,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical,
            ComplianceCoreJsonContext.Default.ApplicationChangePreview);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
