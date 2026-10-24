using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds well-formed engagement lifecycle facts straight onto the engagement owner, exactly as the lifecycle
///     handlers leave them: the change, the complete resulting view and the frozen first response.
/// </summary>
static class ServiceEngagementFacts
{
    static readonly ImmutableContentRef Placeholder = new(Uuid.Parse("00000000-0000-4000-8000-0000000000f3",
        System.Globalization.CultureInfo.InvariantCulture), new string('e', 64), 2, "application/json",
        "first-response-descriptor.v1");

    /// <summary>Creates the draft of an engagement led by an active staff member.</summary>
    public static Task<ServiceEngagementView> CreateAsync(IServiceProvider provider, Uuid tenantId, Uuid engagementId,
        ServiceEngagementDraftContent content, FirmStaffMemberView lead, ActorReference actor, DateTimeOffset at) =>
        ApplyAsync(provider, tenantId, engagementId, engagementId, ServiceEngagementIndependence.CreateKind,
            null, ServiceEngagementRules.Create(tenantId, engagementId, content, lead, actor, at), content, lead,
            null, null, actor, at);

    /// <summary>Closes the open draft of an engagement whose current view is <paramref name="current" />.</summary>
    public static Task<ServiceEngagementView> CloseAsync(IServiceProvider provider, Uuid tenantId, Uuid engagementId,
        ServiceEngagementView current, ActorReference actor, DateTimeOffset at,
        string reason = "Synthetic client closure") => ApplyAsync(provider, tenantId, engagementId, Uuid.CreateVersion4(),
        ServiceEngagementIndependence.CloseKind, (ulong)current.Revision,
        Result<ServiceEngagementView>.Success(ServiceEngagementRules.Close(current, actor, at)), null, null, null, reason,
        actor, at);

    static async Task<ServiceEngagementView> ApplyAsync(IServiceProvider provider, Uuid tenantId, Uuid engagementId,
        Uuid commandId, string kind, ulong? expectedRevision, Result<ServiceEngagementView> built,
        ServiceEngagementDraftContent? draft, FirmStaffMemberView? staff, Uuid? staffMemberId, string? reason,
        ActorReference actor, DateTimeOffset at)
    {
        Assert.True(built.IsSuccess, built.Error?.Message);
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var store = scope.ServiceProvider.GetService<IImmutableContentStore>() ??
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var realm = IndependenceContent.Realm(tenantId);
        var view = built.Value!;
        var viewRef = await IndependenceContent.WriteAsync(store, realm, IndependenceContent.EngagementSchema, view,
            CancellationToken.None);
        var change = await IndependenceContent.WriteAsync(store, realm, IndependenceContent.EngagementChangeSchema,
            new ServiceEngagementChange(kind, draft, staff, staffMemberId ?? staff?.StaffMemberId, reason, actor, at),
            CancellationToken.None);
        var result = await IndependenceContent.WriteResultAsync(store, realm, IndependenceContent.EngagementSchema,
            viewRef, CancellationToken.None);
        var stream = new ServiceEngagementIndependence(tenantId, engagementId).Stream;
        var provenance = new OperationProvenance(commandId, commandId, null, kind, actor, stream, "initial",
            expectedRevision ?? 0, new string('a', 64), at, stream, stream);
        var applied = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, commandId),
            owner => AggregateOutcome.Commit(owner.Change(kind, provenance, change, Placeholder, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        return view;
    }
}
