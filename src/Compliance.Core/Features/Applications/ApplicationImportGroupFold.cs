using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Folds the frozen rows of one target group, in original row order, against the group's single baseline.
///     Create establishes revision one, a link neither overwrites the application nor advances its revision, and a
///     retirement advances it once. The baseline is compared once; every later row is compared against the candidate
///     the earlier rows produce, never against a repeated baseline.
/// </summary>
static class ApplicationImportGroupFold
{
    public static Result<IReadOnlyList<long>> Fold(bool created, bool retired, long revision,
        IReadOnlyList<ApplicationImportPlannedRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var outcomes = new List<long>(rows.Count);
        foreach (var row in rows)
        {
            switch (row.Decision)
            {
                case "create_new" when !created && !retired && revision == 0 && row.ExpectedApplicationRevision is null:
                    (created, revision) = (true, 1);
                    break;
                case "link_existing" when created && !retired && row.ExpectedApplicationRevision == revision:
                    break;
                case "retire" when created && !retired && row.ExpectedApplicationRevision == revision:
                    (retired, revision) = (true, revision + 1);
                    break;
                default:
                    return Result<IReadOnlyList<long>>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The governed import target changed after its frozen plan."));
            }

            outcomes.Add(revision);
        }

        return Result<IReadOnlyList<long>>.Success(outcomes.AsReadOnly());
    }
}
