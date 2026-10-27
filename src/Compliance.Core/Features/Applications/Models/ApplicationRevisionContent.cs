using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Applications.Models;

/// <summary>The normalized resulting content shared by revision validation and advisory comparison.</summary>
sealed record ApplicationRevisionContent(string Name, string Purpose, string? OwnerReference,
    string? Classification, Uuid? SystemOwnerPersonId, Uuid? AccessOwnerPersonId,
    bool IsRestricted, string? ApplicationKind, string? Criticality)
{
    internal static T ResolveField<T>(T value, T current, ApplicationRevisionField intent, bool governed) =>
        governed && !intent.IsSupplied && value is null ? current : value;

    public CommandFailure? Validate(bool governed)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200 ||
            string.IsNullOrWhiteSpace(Purpose) || Purpose.Length > 2000 ||
            OwnerReference is { Length: > 2000 } || Classification is { Length: > 200 })
            return CommandFailure.InvalidContent(
                "An application requires a name of at most 200 characters and purpose of at most 2000; owner reference and classification cannot exceed 2000 and 200 characters.");
        if (ApplicationKind is { Length: 0 or > 200 } || Criticality is { Length: 0 or > 200 } ||
            Classification is { Length: 0 } || SystemOwnerPersonId == Uuid.Empty ||
            AccessOwnerPersonId == Uuid.Empty)
            return CommandFailure.InvalidContent(
                "Classification, owners, application kind and criticality cannot be blank; kind and criticality cannot exceed 200 characters.");
        if (governed && (Classification is null || SystemOwnerPersonId is null ||
            AccessOwnerPersonId is null || ApplicationKind is null || Criticality is null))
            return CommandFailure.InvalidContent(
                "An application requires classification, system and access owners, application kind and criticality.");
        return null;
    }
}
