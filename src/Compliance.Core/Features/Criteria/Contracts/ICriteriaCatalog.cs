using System.Text.RegularExpressions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
namespace Bdgrz.Compliance.Features.Criteria.Contracts;

public interface ICriteriaCatalog
{
    IReadOnlyList<CriteriaCatalogEdition> Editions { get; }
    CriteriaCatalogEdition? GetEdition(Uuid editionId);
    Criterion? GetEntry(Uuid editionId, string identifier);
    IReadOnlyList<Criterion> ListEntries(Uuid editionId, string? category, string? kind,
        string? parentIdentifier);
}
