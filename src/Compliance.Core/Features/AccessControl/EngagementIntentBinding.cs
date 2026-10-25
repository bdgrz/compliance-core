using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>All original admitted terms, including first-owner provenance, are immutable child authority.</summary>
static class EngagementIntentBinding
{
    public static bool Matches(DurableIntentHeader? header, OperationProvenance operation) =>
        header == new DurableIntentHeader(operation.RootOperationId, operation.FirstOwner ?? operation.Owner,
            operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
            operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt);
}
