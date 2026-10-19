using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>A program's exact criteria edition selection as recorded at one revision.</summary>
public sealed record ProgramCriteriaEditionSelection(long Revision, Uuid EditionId,
    DateTimeOffset SelectedAt);
