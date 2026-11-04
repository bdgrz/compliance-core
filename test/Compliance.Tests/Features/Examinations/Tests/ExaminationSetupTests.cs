using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;

namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ExaminationSetupTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid Actor = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("type_i")]
    [InlineData("type_ii")]
    public void ShouldOpenWithUnknownAuditorAndDatesGivenNewExamination(string kind)
    {
        // Arrange
        var owner = New();
        var request = Open(kind);

        // Act
        var failure = owner.Open(request, Actor, "Lead", Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal("open", owner.Current!.Status);
        Assert.Equal(Program, owner.Current.ProgramId);
        Assert.Equal(kind, owner.Current.ExaminationType);
        Assert.Equal("unknown", owner.Current.Auditor.Kind);
        Assert.Null(owner.Current.ConfirmedDates);
        Assert.Single(Pending(owner));
    }

    [Fact]
    public void ShouldPreserveOriginalAcceptanceGivenExactOpenRetryAfterLaterChanges()
    {
        // Arrange
        var owner = New();
        var request = Open("type_i");
        Assert.Null(owner.Open(request, Actor, "Lead", Now));
        Assert.Null(owner.SetTargetDates(new SetExaminationTargetDates(Uuid.CreateVersion4(), Tenant,
            Program, ExaminationId, 1, new ExaminationDates(new DateOnly(2027, 1, 1), null, null)),
            Actor, "Lead", Now.AddMinutes(1)));
        var count = Pending(owner).Count;

        // Act
        var failure = owner.Open(request, Actor, "Changed display", Now.AddHours(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal(count, Pending(owner).Count);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldRefuseChangedIntentGivenConsumedCommandIdentity()
    {
        // Arrange
        var owner = New();
        var request = Open("type_i");
        Assert.Null(owner.Open(request, Actor, "Lead", Now));

        // Act
        var failure = owner.Open(request with { ExaminationType = "type_ii" }, Actor, "Lead", Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Single(Pending(owner));
        Assert.Equal("type_i", owner.Current!.ExaminationType);
    }

    [Fact]
    public void ShouldKeepConfirmationSeparateGivenTargetDateChange()
    {
        // Arrange
        var owner = New();
        Assert.Null(owner.Open(Open("type_i"), Actor, "Lead", Now));
        var confirmed = new ExaminationDates(new DateOnly(2027, 1, 1), null, null);
        Assert.Null(owner.ConfirmDates(new ConfirmExaminationDates(Uuid.CreateVersion4(), Tenant,
            Program, ExaminationId, 1, confirmed, "auditor-email:123"), Actor, "Lead", Now));

        // Act
        var failure = owner.SetTargetDates(new SetExaminationTargetDates(Uuid.CreateVersion4(), Tenant,
            Program, ExaminationId, 2, new ExaminationDates(new DateOnly(2027, 2, 1), null, null)),
            Actor, "Lead", Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(confirmed, owner.Current!.ConfirmedDates);
        Assert.Equal("auditor-email:123", owner.Current.ConfirmationEvidenceReference);
        Assert.NotEqual(owner.Current.TargetDates, owner.Current.ConfirmedDates);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ShouldRefuseConfirmationWithoutFactGivenMissingEvidenceReference(string evidence)
    {
        // Arrange
        var owner = New();
        Assert.Null(owner.Open(Open("type_i"), Actor, "Lead", Now));

        // Act
        var failure = owner.ConfirmDates(new ConfirmExaminationDates(Uuid.CreateVersion4(), Tenant,
            Program, ExaminationId, 1, new ExaminationDates(new DateOnly(2027, 1, 1), null, null), evidence),
            Actor, "Lead", Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Null(owner.Current!.ConfirmedDates);
        Assert.Single(Pending(owner));
    }

    [Fact]
    public void ShouldRefuseReversedPeriodWithoutFactGivenTypeIIExamination()
    {
        // Arrange
        var owner = New();
        Assert.Null(owner.Open(Open("type_ii"), Actor, "Lead", Now));

        // Act
        var failure = owner.SetTargetDates(new SetExaminationTargetDates(Uuid.CreateVersion4(), Tenant,
            Program, ExaminationId, 1, new ExaminationDates(null, new DateOnly(2027, 2, 1),
                new DateOnly(2027, 1, 1))), Actor, "Lead", Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal(1, owner.Revision);
        Assert.Single(Pending(owner));
    }

    [Fact]
    public void ShouldKeepClosedExaminationClosedGivenRepeatedAndReissuedSignOff()
    {
        // Arrange
        var owner = New();
        Assert.Null(owner.Open(Open("type_i"), Actor, "Lead", Now));
        var first = new ExaminationClosure(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.Null(owner.Close(first, Actor, "Signer", Now));
        var firstFact = owner.Current!.Closure;
        var count = Pending(owner).Count;

        // Act
        Assert.Null(owner.Close(first, Actor, "Signer", Now.AddMinutes(1)));
        Assert.Null(owner.Close(new ExaminationClosure(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4()), Actor, "Signer", Now.AddMinutes(2)));
        var mutation = owner.ChangeAuditor(new ChangeExaminationAuditor(Uuid.CreateVersion4(), Tenant,
            Program, ExaminationId, 2, new ExaminationAuditor("external", null, "Other firm")),
            Actor, "Lead", Now);

        // Assert
        Assert.NotNull(mutation);
        Assert.Equal("closed", owner.Current.Status);
        Assert.Equal(firstFact, owner.Current.Closure);
        Assert.Equal(count, Pending(owner).Count);
    }

    [Fact]
    public void ShouldReconstructIdentityAndNamedSignersGivenCommittedSetupFacts()
    {
        // Arrange
        var owner = New();
        Assert.Null(owner.Open(Open("type_i"), Actor, "Lead", Now));
        Assert.Null(owner.NameSigners(new NameExaminationSigners(Uuid.CreateVersion4(), Tenant, Program,
            ExaminationId, 1, Actor, Uuid.CreateVersion4()), Actor, "Admin", Now));
        var facts = Pending(owner).ToArray();

        // Act
        var replayed = new AggregateScenario<Examination>(New()).Given(facts).Aggregate;

        // Assert
        Assert.Equal(owner.Current, replayed.Current);
        Assert.True(replayed.IsNamedSigner(Actor));
        Assert.False(replayed.IsNamedSigner(Uuid.CreateVersion4()));
    }

    static IReadOnlyList<DomainEvent> Pending(Examination owner) => new AggregateScenario<Examination>(owner).PendingEvents;
    static Examination New() => new(Tenant, ExaminationId);
    static OpenExamination Open(string kind) => new(Uuid.CreateVersion4(), Tenant, Program,
        ExaminationId, kind, new ExaminationAuditor("unknown", null, null));
}
