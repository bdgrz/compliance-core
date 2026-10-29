using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     One practice subject an acceptance or staff assignment frozen at admission: a staff member or a canonical user
///     that will be reserved temporarily and committed permanently for <paramref name="Practice" />. The stream is the
///     subject's own owner stream, whose key orders the acquisition.
/// </summary>
public sealed record PracticeSubjectIntent(PracticeSubjectKind Kind, Uuid SubjectId, string Practice,
    EventStreamAddress Stream)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "practice-subject-intent.v1";

    /// <summary>The canonical sort key of the subject's stream.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Key => PracticeSubjects.Key(Stream);
}
