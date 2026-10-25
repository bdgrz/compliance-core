using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds the permanent practice walls of a person straight onto the practice commitment owners, exactly as the
///     acceptance process leaves them: a temporary reservation, the permanent commitment and the settlement of the
///     reservation, for both the staff member and the canonical user.
/// </summary>
static class PracticeCommitmentFacts
{
    static readonly ImmutableContentRef Placeholder = new(Uuid.Parse("00000000-0000-4000-8000-0000000000f4",
        System.Globalization.CultureInfo.InvariantCulture), new string('d', 64), 2, "application/json",
        "engagement-independence-intent.v1");

    /// <summary>Commits the staff member and the canonical user to the practice of one engagement.</summary>
    public static async Task CommitAsync(IServiceProvider provider, Uuid tenantId, Uuid engagementId, Uuid operationId,
        FirmStaffMemberView staff, ActorReference actor, DateTimeOffset at)
    {
        await CommitSubjectAsync(provider, tenantId, PracticeSubjectKind.Staff, staff.StaffMemberId, staff.Practice,
            engagementId, operationId, actor, at);
        await CommitSubjectAsync(provider, tenantId, PracticeSubjectKind.CanonicalUser, staff.UserId, staff.Practice,
            engagementId, operationId, actor, at);
    }

    /// <summary>Reserves, commits and settles one subject for the practice of one engagement.</summary>
    public static async Task CommitSubjectAsync(IServiceProvider provider, Uuid tenantId, PracticeSubjectKind kind,
        Uuid subjectId, string practice, Uuid engagementId, Uuid operationId, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var stream = PracticeSubjects.Stream(tenantId, kind, subjectId);
        OperationProvenance Child(string commandKind) => new(Uuid.CreateVersion4(), operationId, null, commandKind,
            actor, stream, "initial", 1, new string('a', 64), at, stream, stream);
        var reserved = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, kind, subjectId,
            Uuid.Empty, practice, engagementId).ForSourceUse(candidate => new ReservationKey(operationId, stream, 1, candidate.Stream, "actual_staff_assignment")), owner => AggregateOutcome.Commit(owner.Reserve(
            Child(nameof(ReservePracticeIndependence)), Placeholder, 1)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(reserved.IsSuccess, reserved.Error?.Message);
        var committed = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, kind, subjectId).ForSourceUse(candidate => new ReservationKey(operationId, stream, 1, candidate.Stream, "actual_staff_assignment")),
            owner => AggregateOutcome.Commit(owner.Commit(Child(nameof(CommitPracticeIndependence)), Placeholder, 1,
                practice, engagementId)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(committed.IsSuccess, committed.Error?.Message);
        var settled = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, kind, subjectId).ForSourceUse(candidate => new ReservationKey(operationId, stream, 1, candidate.Stream, "actual_staff_assignment")),
            owner => AggregateOutcome.Commit(owner.SettleReservation(
                Child(nameof(SettlePracticeIndependenceReservation)), Placeholder, 1)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
    }
}
