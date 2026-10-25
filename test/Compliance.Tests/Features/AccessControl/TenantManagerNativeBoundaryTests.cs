using System.Reflection;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class TenantManagerNativeBoundaryTests
{
    [Fact]
    public async Task ShouldRetainOnlyLiveAvailableOrSelectedAuthorityGivenManyCompletedAdministratorRemovals()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddAdministratorAsync();
        _ = await host.AddAdministratorAsync();
        for (var index = 0; index < 16; index++)
        {
            var member = await host.AddMemberAsync();
            var assigned = await host.AssignAsync(actor, member);
            Assert.True(assigned.IsSuccess, assigned.Error?.Message);
            Assert.Equal(ProcessState.Completed, (await host.DriveAsync(assigned.Value!.OperationId)).State);
            var removed = await host.RemoveAsync(actor, member);
            Assert.True(removed.IsSuccess, removed.Error?.Message);
            Assert.Equal(ProcessState.Completed, (await host.DriveAsync(removed.Value!.OperationId)).State);
        }

        // Act
        var guard = await host.GuardAsync();
        var history = typeof(TenantManagerGuard).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(guard) is global::System.Collections.ICollection { Count: > 2 })
            .Select(field => field.Name).ToArray();

        // Assert
        Assert.Equal(2, guard.AvailableCount);
        Assert.Equal(0, guard.PendingCount);
        Assert.Empty(history);
    }

    [Fact]
    public async Task ShouldKeepNeutralEpisodeHistoryOutsideDefaultAuthorityGivenManyNonAdministratorSuspensions()
    {
        // Arrange
        await using var host = new AdministratorTransitionHost();
        var actor = await host.AddAdministratorAsync();
        for (var index = 0; index < 16; index++)
        {
            var member = await host.AddMemberAsync();
            var suspended = await host.SuspendAsync(actor, member);
            Assert.True(suspended.IsSuccess, suspended.Error?.Message);
            Assert.Equal(ProcessState.Completed, (await host.DriveAsync(suspended.Value!.OperationId)).State);
            Assert.True((await host.MemberAsync(member)).IsSuspended);
        }

        // Act
        var guard = await host.GuardAsync();
        var history = typeof(TenantManagerGuard).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(guard) is global::System.Collections.ICollection { Count: > 1 })
            .Select(field => field.Name).ToArray();

        // Assert
        Assert.Equal(1, guard.AvailableCount);
        Assert.Equal(0, guard.PendingCount);
        Assert.Empty(history);
    }
}
