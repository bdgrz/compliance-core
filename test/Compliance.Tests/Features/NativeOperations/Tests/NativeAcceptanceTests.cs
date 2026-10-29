using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

public sealed class NativeAcceptanceTests
{
    static readonly Uuid Logical = Uuid.Parse("00000000-0000-4000-8000-0000000000c1", CultureInfo.InvariantCulture);
    static readonly Uuid Other = Uuid.Parse("00000000-0000-4000-8000-0000000000c2", CultureInfo.InvariantCulture);
    static readonly EventStreamAddress Owner = new("tenant-a", "probes", "p1");
    static readonly ActorReference Actor = new("member", "m1", "Member One");
    static readonly ImmutableContentRef Response = new(Uuid.CreateVersion4(), new string('a', 64), 10,
        "application/json", FirstResponses.Schema);

    static OperationProvenance Provenance(Uuid logicalId, string kind = "Admit") => new(logicalId, logicalId, null,
        kind, Actor, Owner, "initial", 0, new string('d', 64), new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero),
        Owner, Owner);

    [Fact]
    public void ShouldKeepTwoCommandKindsApartGivenOneLogicalIdAndASelectedKind()
    {
        // Arrange
        var start = new NativeAcceptance(Logical, "Start");
        var complete = new NativeAcceptance(Logical, "Complete");
        var facts = new[]
        {
            new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical, "Start"), Response),
            new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical, "Complete"), Response),
        };

        // Act
        foreach (var fact in facts)
        {
            start.Observe(fact);
            complete.Observe(fact);
        }

        // Assert
        Assert.Same(facts[0], start.SelectedFact);
        Assert.Same(facts[1], complete.SelectedFact);
        Assert.Equal(NativeAcceptanceDecision.Replayed, start.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Start"))));
        Assert.Equal(NativeAcceptanceDecision.Replayed, complete.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Complete"))));
    }

    [Fact]
    public void ShouldDecideFromProvenanceGivenAFactThatIsNotNative()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        var fact = new ProbeEdited(Uuid.CreateVersion4(), "edited");

        // Act
        acceptance.Observe(fact, Provenance(Logical));
        var same = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical)));
        var changed = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Other")));

        // Assert
        Assert.Same(fact, acceptance.SelectedFact);
        Assert.Equal(NativeAcceptanceDecision.Replayed, same);
        Assert.Equal(NativeAcceptanceDecision.Conflict, changed);
    }

    [Fact]
    public void ShouldReplayGivenAcceptedFactWithEqualBinding()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical), Response));

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical)));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
    }

    [Fact]
    public void ShouldReplayOriginalFactGivenOnlyActorDisplayChanged()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        var original = Provenance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", original, Response));
        var candidate = original with { Actor = Actor with { Display = "Updated profile label" } };

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(candidate));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Replayed, decision);
        Assert.Equal(Actor, acceptance.SelectedProvenance!.Actor);
    }

    [Theory]
    [InlineData("member", "other-member")]
    [InlineData("platform_operator", "m1")]
    public void ShouldConflictGivenActorIdentityChanged(string kind, string id)
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        var original = Provenance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", original, Response));
        var candidate = original with { Actor = Actor with { Kind = kind, Id = id } };

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(candidate));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    [Fact]
    public void ShouldConflictGivenAcceptedFactWithChangedKind()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical), Response));

        // Act
        var decision = acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical, "Revise")));

        // Assert
        Assert.Equal(NativeAcceptanceDecision.Conflict, decision);
    }

    [Fact]
    public void ShouldIgnoreFactsGivenOtherLogicalIds()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);

        // Act
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Other), Response));

        // Assert
        Assert.Null(acceptance.SelectedFact);
        Assert.Equal(NativeAcceptanceDecision.None, acceptance.Decide(AcceptedOperationBinding.Of(Provenance(Logical))));
    }

    [Fact]
    public void ShouldThrowGivenSecondAcceptedFactForSelectedLogicalId()
    {
        // Arrange
        var acceptance = new NativeAcceptance(Logical);
        acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "p", Provenance(Logical), Response));

        // Act
        var failure = Record.Exception(() =>
            acceptance.Observe(new ProbeAdmitted(Uuid.CreateVersion4(), "q", Provenance(Logical), Response)));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }
}
