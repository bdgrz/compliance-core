using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Features.Applications.Services;

/// <summary>Verifies the frozen import header against the exact native root that admitted it.</summary>
static class ApplicationImportIntentBindings
{
    public static DurableIntentHeader HeaderOf(OperationProvenance operation) =>
        new(operation.RootOperationId, operation.FirstOwner ?? operation.Owner, operation.AffectedOwner,
            operation.ExpectedRevisionOwner, operation.Actor, operation.Episode, operation.OriginalExpectedRevision,
            operation.AuthoredIntentSha256, operation.AcceptedAt);

    public static void Require(DurableIntentHeader header, OperationProvenance root)
    {
        if (header is null || header.RootOperationId != root.RootOperationId || header.FirstOwner != (root.FirstOwner ?? root.Owner) ||
            header.AffectedOwner != root.AffectedOwner || header.ExpectedRevisionOwner != root.ExpectedRevisionOwner ||
            header.OriginalActor != root.Actor || header.Episode != root.Episode ||
            header.OriginalExpectedRevision != root.OriginalExpectedRevision ||
            header.AuthoredIntentSha256 != root.AuthoredIntentSha256 || header.AcceptedAt != root.AcceptedAt)
            throw new ImmutableContentIntegrityException("The frozen import header differs from its exact native first-owner root.");
    }
}
