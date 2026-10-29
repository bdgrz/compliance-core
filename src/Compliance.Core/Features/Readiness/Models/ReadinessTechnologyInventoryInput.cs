using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>The selected as-of revision of a technology record referenced by the program boundary.</summary>
public sealed record ReadinessTechnologyInventoryInput(string SubjectType, Uuid RecordId,
    long? Revision, string? Lifecycle, DateTimeOffset? LastChangedAt,
    DateOnly? EffectiveFrom = null);
