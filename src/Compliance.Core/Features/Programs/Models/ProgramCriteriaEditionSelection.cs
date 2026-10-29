using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>A program's exact criteria edition selection as recorded at one revision.</summary>
public sealed record ProgramCriteriaEditionSelection(long Revision, Uuid EditionId,
    DateTimeOffset SelectedAt);
