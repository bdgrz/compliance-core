namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Explicit verified person values at one exact owner cut.</summary>
public sealed record PersonReadSnapshot(Person Owner, PersonView? View)
{
    public PersonRules Rules => new(Owner.TenantId, Owner.Id, View);
}
