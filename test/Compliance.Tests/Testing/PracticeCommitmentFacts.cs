using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
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
            actor, stream, "initial", 1, new string('a', 64), at, stream, stream, stream);
        var key = new ReservationKey(operationId, stream, 1, stream, "actual_staff_assignment");
        var reserve = Child(nameof(ReservePracticeIndependence));
        var commit = Child(nameof(CommitPracticeIndependence));
        var settle = Child(nameof(SettlePracticeIndependenceReservation));
        var reserved = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, kind, subjectId,
            reserve.LogicalId, practice, engagementId).ForSourceUses(key), owner => AggregateOutcome.Commit(owner.Reserve(
            reserve, Placeholder, 1)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(reserved.IsSuccess, reserved.Error?.Message);
        var committed = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, kind, subjectId,
            commit.LogicalId, selectedReservation: key).ForSourceUses(key), owner => AggregateOutcome.Commit(owner.Commit(
            commit, Placeholder, 1, practice, engagementId)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(committed.IsSuccess, committed.Error?.Message);
        var settled = await executor.ExecuteAsync(new PracticeIndependenceCommitment(tenantId, kind, subjectId,
            settle.LogicalId).ForSourceUses(key), owner => AggregateOutcome.Commit(owner.SettleReservation(
            settle, Placeholder, 1)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
    }
}
