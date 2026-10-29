using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Projections;

static class ObservationPeriodDirectorySchema
{
    // OpenedAt is pinned to the creation fact in this identity-only directory and is never replaced by lifecycle data.
    public static readonly KvDirectoryIndex<ObservationPeriodIdentity> ByProgramOpened = new(
        "by_program_opened", 1, static package =>
        [
            package.ProgramId.ToString(),
            package.OpenedAt.UtcTicks.ToString("D20", CultureInfo.InvariantCulture),
            package.ObservationPeriodId.ToString(),
        ]);

    public static readonly KvDirectory<ObservationPeriodIdentity, Uuid> Periods = new("periods",
        ComplianceCoreJsonContext.Default.ObservationPeriodIdentity, static package => package.ObservationPeriodId,
        static id => [id.ToString()], [ByProgramOpened]);
    public readonly record struct CloseKey(Uuid CloseId, Uuid PeriodId);
    public static readonly KvDirectoryIndex<ObservationPeriodCloseIdentity> ByProgramClose = new(
        "by_program_close", 1, static close => [close.ProgramId.ToString(), close.CloseId.ToString(), close.PeriodId.ToString()]);
    public static readonly KvDirectory<ObservationPeriodCloseIdentity, CloseKey> Closes = new("closes",
        ComplianceCoreJsonContext.Default.ObservationPeriodCloseIdentity, static close => new CloseKey(close.CloseId, close.PeriodId),
        static key => [key.CloseId.ToString(), key.PeriodId.ToString()], [ByProgramClose]);
}
