using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Applications.Services;

static class ApplicationRelationshipIdentity
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
