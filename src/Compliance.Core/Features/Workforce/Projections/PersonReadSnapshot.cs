using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Workforce.Rules;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Projections;

/// <summary>Explicit verified person values at one exact owner cut.</summary>
public sealed record PersonReadSnapshot(Person Owner, PersonView? View)
{
    public PersonRules Rules => new(Owner.TenantId, Owner.Id, View);
}
