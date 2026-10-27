using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Operations.Models;
using System.Globalization;
using Cntryl.Portia;

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
