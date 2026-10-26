using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

internal static class ApplicationRelationshipIdentity
{
    public const string DependsOn = "depends_on";
    public const string Replaces = "replaces";

    public static bool IsImpactDigest(string? digest) => digest is { Length: 64 } &&
        digest.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    public static bool IsSupported(string relationshipType) =>
        relationshipType is DependsOn or Replaces;

    public static Uuid Id(Uuid sourceApplicationId, string relationshipType,
        Uuid targetApplicationId) => Uuid.CreateVersion5(sourceApplicationId,
        $"application-relationship:{relationshipType}:{targetApplicationId}");
}
