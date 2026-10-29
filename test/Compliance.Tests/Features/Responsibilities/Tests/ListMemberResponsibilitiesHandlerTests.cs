using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Contracts;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Responsibilities.Handlers;
using Bdgrz.Compliance.Features.Responsibilities.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;

public sealed class ListMemberResponsibilitiesHandlerTests
{
    [Fact]
    public async Task ShouldReturnCurrentAndScheduledOpenAssignmentsGivenPastFutureAndRevokedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var now = DateTimeOffset.Parse("2026-09-29T12:00:00Z", CultureInfo.InvariantCulture);
        var assignments = new[]
        {
            CreateAssignment(tenantId, memberId, now.AddDays(-1), null, null),
            CreateAssignment(tenantId, memberId, now.AddDays(-2), now.AddHours(-1), null),
            CreateAssignment(tenantId, memberId, now.AddHours(1), null, null),
            CreateAssignment(tenantId, memberId, now.AddDays(-1), null, now.AddMinutes(-1)),
        };
        var index = new FixedMemberResponsibilityIndex(assignments);
        var handler = new ListMemberResponsibilitiesHandler(index, new FixedTimeProvider(now));
        var request = new ListMemberResponsibilities(tenantId, userId);
        var context = new RequestContext<ListMemberResponsibilities>(request, new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, item => item.AssignmentId == assignments[0].AssignmentId);
        Assert.Contains(result.Value, item => item.AssignmentId == assignments[2].AssignmentId);
        Assert.Equal(tenantId, index.LastTenantId);
        Assert.Equal(memberId, index.LastMemberId);
    }

    static ResponsibilityAssignmentView CreateAssignment(Uuid tenantId, Uuid memberId,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil, DateTimeOffset? revokedAt) =>
        new(tenantId, Uuid.CreateVersion4(), memberId, ResponsibilityType.ControlOwner,
            new ResponsibilityScope("boundary", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1),
            effectiveFrom, Uuid.CreateVersion4(), effectiveFrom, effectiveUntil, revokedAt,
            revokedAt is null ? Uuid.Empty : Uuid.CreateVersion4(), [], "Admin");

    sealed class FixedMemberResponsibilityIndex(IReadOnlyList<ResponsibilityAssignmentView> assignments)
        : IMemberResponsibilityIndex
    {
        public Uuid LastTenantId { get; private set; }
        public Uuid LastMemberId { get; private set; }

        public ValueTask<IReadOnlyList<ResponsibilityAssignmentView>> GetAsync(Uuid tenantId,
            Uuid memberId, CancellationToken ct = default)
        {
            LastTenantId = tenantId;
            LastMemberId = memberId;
            return ValueTask.FromResult(assignments);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
