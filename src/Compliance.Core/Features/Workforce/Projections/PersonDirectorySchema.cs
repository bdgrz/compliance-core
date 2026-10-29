using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Projections;

static class PersonDirectorySchema
{
    public static readonly KvDirectoryIndex<PersonView> ByDisplayName = new(
        "by_display_name", 1,
        static person => [person.DisplayName.ToUpperInvariant(), person.PersonId.ToString()]);

    public static readonly KvDirectoryIndex<PersonView> ByCorrelatedUser = new(
        "by_correlated_user", 1,
        static person => [person.CorrelatedUserId?.ToString() ?? string.Empty, person.PersonId.ToString()]);

    public static readonly KvDirectory<PersonView, Uuid> People = new(
        "people", ComplianceCoreJsonContext.Default.PersonView,
        static person => person.PersonId,
        static personId => [personId.ToString()], [ByDisplayName, ByCorrelatedUser]);
}
