using Bdgrz.Compliance.Features.SourceUse;
using System.Reflection;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class PracticeIndependenceCommitmentTests
{
    static readonly ImmutableContentRef Intent = new(Uuid.Parse("00000000-0000-4000-8000-0000000000f5",
        System.Globalization.CultureInfo.InvariantCulture), new string('c', 64), 2, "application/json",
        "engagement-independence-intent.v1");

    [Fact]
    public async Task ShouldKeepIndependentPracticeReservationsGivenSameRootOnDifferentFirstOwners()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        var root = Uuid.CreateVersion4();
        var stream = PracticeSubjects.Stream(host.Tenant, PracticeSubjectKind.Staff, subject);
        var executor = host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        async Task ApplyAsync(Uuid engagement)
        {
            var firstOwner = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
            var key = new ReservationKey(root, firstOwner, 1, stream, "actual_staff_assignment");
            OperationProvenance Child(string kind) => new(Uuid.CreateVersion4(), root, null, kind,
                host.MemberReference, stream, "initial", 1, new string('a', 64), IndependenceHost.At, stream, stream, firstOwner);
            var reserve = Child(nameof(ReservePracticeIndependence));
            var commit = Child(nameof(CommitPracticeIndependence));
            var settle = Child(nameof(SettlePracticeIndependenceReservation));
            var held = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff,
                subject, reserve.LogicalId, "attest", engagement).ForSourceUses(key), owner =>
                AggregateOutcome.Commit(owner.Reserve(reserve, Intent, 1)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(held.IsSuccess, held.Error?.Message);
            var committed = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff,
                subject, commit.LogicalId).ForSourceUses(key), owner => AggregateOutcome.Commit(owner.Commit(commit, Intent, 1,
                "attest", engagement)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(committed.IsSuccess, committed.Error?.Message);
            var settled = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff,
                subject, settle.LogicalId).ForSourceUses(key), owner => AggregateOutcome.Commit(owner.SettleReservation(settle, Intent, 1)),
                new RequestDispatchContext(RequestActor.System), CancellationToken.None);
            Assert.True(settled.IsSuccess, settled.Error?.Message);
        }
        await ApplyAsync(Uuid.CreateVersion4());

        // Act
        var failure = await Record.ExceptionAsync(() => ApplyAsync(Uuid.CreateVersion4()));

        // Assert
        Assert.Null(failure);
        Assert.Equal(2UL, (await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff, subject))).Revision);
    }

    [Fact]
    public async Task ShouldKeepBoundedPracticeAuthorityGivenManySettledPermanentCommitments()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        for (var index = 0; index < 32; index++)
            await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.CanonicalUser,
                subject, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference, IndependenceHost.At);

        // Act
        var owner = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.CanonicalUser, subject));
        var growing = typeof(PracticeIndependenceCommitment).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is global::System.Collections.ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(growing);
        Assert.Equal(32UL, owner.Revision);
        Assert.True(owner.Serves("attest"));
        Assert.False(owner.Uses.HasActiveUse);
    }

    [Fact]
    public void ShouldKeepStaffAndCanonicalUserSubjectsApartGivenSameIdentityAndOrderPracticeBeforeServiceEpoch()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var subject = Uuid.CreateVersion4();

        // Act
        var staff = new PracticeIndependenceCommitment(tenant, PracticeSubjectKind.Staff, subject);
        var user = new PracticeIndependenceCommitment(tenant, PracticeSubjectKind.CanonicalUser, subject);
        var again = new PracticeIndependenceCommitment(tenant, PracticeSubjectKind.Staff, subject);
        var other = new PracticeIndependenceCommitment(Uuid.CreateVersion4(), PracticeSubjectKind.Staff, subject);

        // Assert
        Assert.NotEqual(staff.Id, user.Id);
        Assert.Equal(staff.Id, again.Id);
        Assert.NotEqual(staff.Id, other.Id);
        Assert.Equal(AggregateIdentity.Create(nameof(PracticeIndependenceCommitment), tenant, "staff", subject), staff.Id);
        Assert.Equal(new EventStreamAddress(tenant.ToString(), "client-practice-commitments", $"staff:{subject}"),
            staff.Stream);
        Assert.Equal(new EventStreamAddress(tenant.ToString(), "client-practice-commitments",
            $"canonical_user:{subject}"), user.Stream);
        Assert.True(StreamKeyOrder.Instance.Compare(PracticeSubjects.Key(staff.Stream),
            PracticeSubjects.Key(new ClientServiceIndependence(tenant).Stream)) < 0);
    }

    [Fact]
    public async Task ShouldCommitPermanentlyGivenExactActiveReservationAndKeepItAfterSettlement()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (subject, engagement, operation) = (Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff, subject,
            "attest", engagement, operation, host.MemberReference, IndependenceHost.At);
        var owner = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff,
            subject).ForSourceUse(candidate => new ReservationKey(operation, candidate.Stream, 1, candidate.Stream, "actual_staff_assignment")));

        // Assert
        Assert.Equal("attest", owner.Practice);
        Assert.True(owner.IsCommitted);
        Assert.Equal(1UL, owner.Revision);
        var history = await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner);
        var committed = Assert.Single(history.Commitments);
        Assert.Equal(engagement, committed.EngagementId);
        Assert.Equal(operation, committed.Operation.RootOperationId);
        Assert.False(owner.Uses.HasActiveUse);
        Assert.True(owner.Uses.Find(new Bdgrz.Compliance.Features.SourceUse.ReservationKey(operation,
            owner.Stream, 1, owner.Stream, "actual_staff_assignment")) is { IsSettled: true, GrantFact: not null });
        Assert.True(owner.Serves("attest"));
        Assert.False(owner.Serves("advisory"));
    }

    [Fact]
    public async Task ShouldRefuseCommitmentGivenNoActiveReservationOfTheSameOperation()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        var executor = host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var stream = PracticeSubjects.Stream(host.Tenant, PracticeSubjectKind.Staff, subject);
        var provenance = new OperationProvenance(Uuid.CreateVersion4(), Uuid.CreateVersion4(), null,
            nameof(CommitPracticeIndependence), host.MemberReference, stream, "initial", 1, new string('a', 64),
            IndependenceHost.At, stream, stream);

        // Act
        var unreserved = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.Staff, subject), owner => AggregateOutcome.Commit(owner.Commit(provenance, Intent, 1,
            "attest", Uuid.CreateVersion4())), new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(unreserved.IsSuccess);
        Assert.True(unreserved.Error!.IsTransient);
        Assert.Empty(await host.EventsAsync<PracticeIndependenceCommitted>(stream));
    }

    [Fact]
    public async Task ShouldRefuseReservationAndCommitmentGivenPermanentOppositePractice()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.CanonicalUser,
            subject, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference, IndependenceHost.At);
        var owner = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.CanonicalUser, subject));

        // Act
        var opposite = owner.PrepareReservation(Uuid.CreateVersion4(), "advisory");
        var same = owner.PrepareReservation(Uuid.CreateVersion4(), "attest");
        var invalid = owner.PrepareReservation(Uuid.CreateVersion4(), "unknown");

        // Assert
        Assert.False(opposite.IsSuccess);
        Assert.False(opposite.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, opposite.Error.Kind);
        Assert.True(same.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
    }

    [Fact]
    public async Task ShouldWaitAndAllowTheSamePracticeGivenAnotherOperationHoldsTheOppositePractice()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var subject = Uuid.CreateVersion4();
        var executor = host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var stream = PracticeSubjects.Stream(host.Tenant, PracticeSubjectKind.Staff, subject);
        OperationProvenance Reserve(Uuid operation) => new(Uuid.CreateVersion4(), operation, null,
            nameof(ReservePracticeIndependence), host.MemberReference, stream, "initial", 1, new string('a', 64),
            IndependenceHost.At, stream, stream);
        var holder = Uuid.CreateVersion4();
        var held = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff,
            subject, Uuid.Empty, "attest", Uuid.CreateVersion4()), owner => AggregateOutcome.Commit(
            owner.Reserve(Reserve(holder), Intent, 1)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(held.IsSuccess, held.Error?.Message);

        // Act
        var opposite = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.Staff, subject, Uuid.Empty, "advisory", Uuid.CreateVersion4()), owner =>
            AggregateOutcome.Commit(owner.Reserve(Reserve(Uuid.CreateVersion4()), Intent, 1)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var sameOperation = Uuid.CreateVersion4();
        var sameProvenance = Reserve(sameOperation);
        var same = await executor.ExecuteAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.Staff, subject, sameProvenance.LogicalId, "attest", Uuid.CreateVersion4()).ForSourceUse(candidate =>
                new ReservationKey(sameOperation, stream, 1, candidate.Stream, "actual_staff_assignment")), owner =>
            AggregateOutcome.Commit(owner.Reserve(sameProvenance, Intent, 1)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        Assert.False(opposite.IsSuccess);
        Assert.True(opposite.Error!.IsTransient);
        Assert.True(same.IsSuccess, same.Error?.Message);
    }

    [Fact]
    public void ShouldOfferNoReleaseOperationGivenPermanentCommitmentOwner()
    {
        // Arrange
        var forbidden = new[] { "Release", "Revoke", "Remove", "Delete", "Withdraw", "Expire", "Reactivate", "Clear" };

        // Act
        var operations = typeof(PracticeIndependenceCommitment)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(method => method.Name).ToArray();

        // Assert
        Assert.NotEmpty(operations);
        Assert.All(operations, name => Assert.DoesNotContain(forbidden, word =>
            name.Contains(word, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(["Commit", "PrepareReservation", "Reserve", "SettleReservation"], operations
            .Where(name => name is "Commit" or "PrepareReservation" or "Reserve" or "SettleReservation")
            .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ShouldRefuseOppositePracticeAssignmentGivenStaffOrCanonicalUserWallInTheCommitmentOwner()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var wall = host.Scope.ServiceProvider.GetRequiredService<PracticeIndependenceWall>();
        var staff = await host.RegisterStaffAsync("attest");
        var other = await host.RegisterStaffAsync("attest");
        var engagement = Uuid.CreateVersion4();
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
            staff.StaffMemberId, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference,
            IndependenceHost.At);
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.CanonicalUser,
            other.UserId, "attest", Uuid.CreateVersion4(), Uuid.CreateVersion4(), host.MemberReference,
            IndependenceHost.At);

        // Act
        var staffConflict = await wall.CanAssignAsync(host.Tenant, new EngagementAssignment(host.Tenant, engagement,
            staff.StaffMemberId, staff.UserId, EngagementPractice.Advisory));
        var userConflict = await wall.CanAssignAsync(host.Tenant, new EngagementAssignment(host.Tenant, engagement,
            other.StaffMemberId, other.UserId, EngagementPractice.Advisory));
        var samePractice = await wall.CanAssignAsync(host.Tenant, new EngagementAssignment(host.Tenant, engagement,
            staff.StaffMemberId, staff.UserId, EngagementPractice.Attest));
        var otherClient = await wall.CanAssignAsync(Uuid.CreateVersion4(), new EngagementAssignment(
            Uuid.CreateVersion4(), engagement, staff.StaffMemberId, staff.UserId, EngagementPractice.Advisory));
        var invalid = await wall.CanAssignAsync(host.Tenant, new EngagementAssignment(host.Tenant, engagement,
            Uuid.Empty, staff.UserId, EngagementPractice.Advisory));

        // Assert
        Assert.Equal(IndependenceDecisionCode.PersonPracticeConflict, staffConflict.Code);
        Assert.Equal(IndependenceDecisionCode.PersonPracticeConflict, userConflict.Code);
        Assert.Equal(IndependenceCompartments.PersonExclusivityRuleId, staffConflict.RuleId);
        Assert.Equal(IndependenceDecisionCode.Allowed, samePractice.Code);
        Assert.Equal(IndependenceDecisionCode.AssignmentInvalid, otherClient.Code);
        Assert.Equal(IndependenceDecisionCode.AssignmentInvalid, invalid.Code);
        Assert.True(await wall.ServesAsync(host.Tenant, other.UserId, EngagementPractice.Attest));
        Assert.False(await wall.ServesAsync(host.Tenant, other.UserId, EngagementPractice.Advisory));
        Assert.Single(await wall.AssignmentsOfUserAsync(host.Tenant, other.UserId));
        Assert.Empty(await wall.AssignmentsOfUserAsync(Uuid.CreateVersion4(), other.UserId));
    }

    [Fact]
    public async Task ShouldDenyAttestAuthorshipAndAdvisoryCompartmentGivenCommitmentOwnerWallEvenWhenEngagementClosed()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(host.Provider, host.Tenant,
            Uuid.CreateVersion4(), revoked: true);
        var management = host.Scope.ServiceProvider.GetRequiredService<ClientManagementIndependenceGuard>();
        var compartments = host.Scope.ServiceProvider.GetRequiredService<ClientCompartmentIndependenceGuard>();

        // Act
        var authorship = await management.CanAuthorAsync(host.Tenant, staff.UserId);
        var advisory = await compartments.CanReadAsync(host.Tenant, staff.UserId, RecordCompartment.AdvisoryWorkingNotes);
        var attest = await compartments.CanReadAsync(host.Tenant, staff.UserId, RecordCompartment.AttestInternal);
        var otherClient = await management.CanAuthorAsync(Uuid.CreateVersion4(), staff.UserId);
        var stranger = await management.CanAuthorAsync(host.Tenant, Uuid.CreateVersion4());

        // Assert
        Assert.False(authorship);
        Assert.False(advisory);
        Assert.True(attest);
        Assert.True(otherClient);
        Assert.True(stranger);
    }
}
