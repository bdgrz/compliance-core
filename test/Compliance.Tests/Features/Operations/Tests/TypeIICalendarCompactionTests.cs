using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class TypeIICalendarCompactionTests
{
    [Fact]
    public async Task ShouldKeepCalendarOwnerBoundedGivenLifetimeReleasedReservations()
    {
        // Arrange
        const int count = 32;
        await using var fixture = new TypeIIRangeFixture();
        var roots = new List<Uuid>();
        for (var index = 0; index < count; index++)
        {
            var root = await fixture.StartPeriodAsync(Uuid.CreateVersion4());
            roots.Add(root);
            await fixture.FreezeCloseAsync(root);
            await fixture.Host.ReleaseAsync<ObservationPeriodCloseFrozen>(fixture.StreamOf(root));
        }

        // Act
        var ordinary = await fixture.CalendarAsync();
        var owner = ordinary.Owner;
        var selected = await fixture.CalendarAsync(roots[0], roots[^1]);
        var facts = await fixture.Host.EventsAsync<DomainEvent>(fixture.CalendarStream);
        var retained = typeof(TypeIIObservationCalendar).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 0 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(count * 3, owner.Revision);
        Assert.Equal(count, owner.ReservationGeneration);
        Assert.DoesNotContain(ordinary.Reservations, value => value.IsLive);
        Assert.Empty(ordinary.History);
        Assert.Equal(count, ordinary.Authority.TerminalCount);
        Assert.Null(ordinary.Find(roots[0]));
        Assert.Equal(TypeIIRangeReservationState.Released, selected.Find(roots[0])!.State);
        Assert.Equal(TypeIIRangeReservationState.Released, selected.Find(roots[^1])!.State);
        Assert.Equal(facts[0].Metadata, selected.Find(roots[0])!.AuthorizedFact!.Metadata);
        Assert.Equal(facts[^1].Metadata, selected.Find(roots[^1])!.SettledFact!.Metadata);
        Assert.Equal(count * 3, facts.Count);
    }
}
