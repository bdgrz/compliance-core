using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
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

    /// <summary>
    ///     Accepts the open draft with the snapshot <paramref name="acceptance" />: the root admission, the irreversible
    ///     authorization and the recorded acceptance, exactly as the ordered process leaves them on the owner. Returns
    ///     the identity of the acceptance operation, which the practice walls are committed under.
    /// </summary>
    public static async Task<Uuid> AcceptAsync(IServiceProvider provider, Uuid tenantId, Uuid engagementId,
        ServiceEngagementAcceptanceView acceptance, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var store = scope.ServiceProvider.GetService<IImmutableContentStore>() ??
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var realm = IndependenceContent.Realm(tenantId);
        var stream = new ServiceEngagementIndependence(tenantId, engagementId).Stream;
        var operationId = Uuid.CreateVersion4();
        var view = await IndependenceContent.WriteAsync(store, realm, EngagementAcceptanceEffects.AcceptanceSchema,
            acceptance, CancellationToken.None);
        var result = await IndependenceContent.WriteResultAsync(store, realm, EngagementAcceptanceEffects.ResultSchema,
            view, CancellationToken.None);
        var context = new RequestDispatchContext(RequestActor.System);
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId));
        var rootRevision = current.Revision;
        Uuid? cause = null;
        OperationProvenance Provenance(Uuid logicalId, string kind, ulong? expected) => new(logicalId, operationId, cause,
            kind, actor, stream, "initial", expected ?? rootRevision, new string('a', 64), at, stream, stream, FirstOwner: stream);
        var admitted = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, operationId),
            owner => AggregateOutcome.Commit(owner.AdmitOperation(Provenance(operationId,
                ServiceEngagementIndependence.AcceptKind, current.Revision), Placeholder, current.Revision, Placeholder,
                EngagementIndependenceOperationKind.Acceptance)), context, CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var admittedOwner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId, selectedRootId: operationId));
        cause = admittedOwner.OperationOf(operationId)!.Requested.Metadata.EventId;
        var authorizationId = Uuid.CreateVersion4();
        var authorized = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, authorizationId, operationId),
            owner => AggregateOutcome.Commit(owner.AuthorizeOperation(Provenance(authorizationId,
                nameof(AuthorizeEngagementPracticeCommitments), null), at, Placeholder)), context,
            CancellationToken.None);
        Assert.True(authorized.IsSuccess, authorized.Error?.Message);
        var authorizedOwner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId, selectedRootId: operationId));
        cause = authorizedOwner.OperationOf(operationId)!.Authorized!.Metadata.EventId;
        var completionId = Uuid.CreateVersion4();
        var recorded = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, completionId, operationId),
            owner => AggregateOutcome.Commit(owner.RecordAcceptance(Provenance(completionId,
                nameof(CompleteServiceEngagementAcceptance), null), view, result)), context, CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        return operationId;
    }

    /// <summary>
    ///     Records one actual staff assignment on the accepted engagement: the root admission, the irreversible
    ///     authorization and the recorded assignment, exactly as the ordered process leaves them on the owner.
    /// </summary>
    public static async Task<(Uuid Operation, ServiceEngagementAcceptanceView View)> AssignAsync(IServiceProvider provider,
        Uuid tenantId, Uuid engagementId, ServiceEngagementAcceptanceView current, FirmStaffMemberView staff,
        ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var store = scope.ServiceProvider.GetService<IImmutableContentStore>() ??
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var realm = IndependenceContent.Realm(tenantId);
        var stream = new ServiceEngagementIndependence(tenantId, engagementId).Stream;
        var operationId = Uuid.CreateVersion4();
        var effectiveFrom = DateOnly.FromDateTime(at.UtcDateTime);
        var revised = EngagementAcceptanceViews.Assigned(current, staff, effectiveFrom, actor, at);
        var staffReference = await FirmStaffContent.WriteStaffAsync(store, Uuid.CreateVersion4(), staff,
            CancellationToken.None);
        var view = await IndependenceContent.WriteAsync(store, realm, EngagementAcceptanceEffects.AcceptanceSchema,
            revised, CancellationToken.None);
        var result = await IndependenceContent.WriteResultAsync(store, realm, EngagementAcceptanceEffects.ResultSchema,
            view, CancellationToken.None);
        var context = new RequestDispatchContext(RequestActor.System);
        var owner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId));
        var rootRevision = owner.Revision;
        Uuid? cause = null;
        OperationProvenance Provenance(Uuid logicalId, string kind, ulong? expected) => new(logicalId, operationId, cause,
            kind, actor, stream, "initial", expected ?? rootRevision, new string('a', 64), at, stream, stream, FirstOwner: stream);
        var admitted = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, operationId),
            item => AggregateOutcome.Commit(item.AdmitOperation(Provenance(operationId,
                ServiceEngagementIndependence.AssignKind, owner.Revision), Placeholder, owner.Revision, Placeholder,
                EngagementIndependenceOperationKind.StaffAssignment)), context, CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var admittedOwner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId, selectedRootId: operationId));
        cause = admittedOwner.OperationOf(operationId)!.Requested.Metadata.EventId;
        var authorizationId = Uuid.CreateVersion4();
        var authorized = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, authorizationId, operationId),
            item => AggregateOutcome.Commit(item.AuthorizeOperation(Provenance(authorizationId,
                nameof(AuthorizeEngagementPracticeCommitments), null), at, Placeholder)), context,
            CancellationToken.None);
        Assert.True(authorized.IsSuccess, authorized.Error?.Message);
        var authorizedOwner = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId, selectedRootId: operationId));
        cause = authorizedOwner.OperationOf(operationId)!.Authorized!.Metadata.EventId;
        var completionId = Uuid.CreateVersion4();
        var recorded = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, completionId, operationId),
            item => AggregateOutcome.Commit(item.RecordStaffAssignment(Provenance(completionId,
                nameof(CompleteServiceEngagementStaffAssignment), null), staffReference, effectiveFrom, Placeholder,
                Placeholder, 1, actor, at, result)), context, CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        return (operationId, revised);
    }

    /// <summary>Closes the accepted engagement; the practice walls it committed are not touched.</summary>
    public static async Task CloseAcceptedAsync(IServiceProvider provider, Uuid tenantId, Uuid engagementId,
        ServiceEngagementAcceptanceView acceptance, ActorReference actor, DateTimeOffset at,
        string reason = "Synthetic client closure")
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var store = scope.ServiceProvider.GetService<IImmutableContentStore>() ??
            new FitzKvImmutableContentStore(new PersistenceKvClient(new InMemoryKvClient()));
        var realm = IndependenceContent.Realm(tenantId);
        var stream = new ServiceEngagementIndependence(tenantId, engagementId).Stream;
        var commandId = Uuid.CreateVersion4();
        var revised = ServiceEngagementRules.Revoke(acceptance, Uuid.Empty, null, reason, actor, at);
        var view = await IndependenceContent.WriteAsync(store, realm, IndependenceContent.RevocationSchema, revised,
            CancellationToken.None);
        var result = await IndependenceContent.WriteResultAsync(store, realm, IndependenceContent.RevocationSchema,
            view, CancellationToken.None);
        var current = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenantId, engagementId));
        var provenance = new OperationProvenance(commandId, commandId, null, "RevokeServiceEngagementActualStaff", actor,
            stream, "initial", current.Revision, new string('a', 64), at, stream, stream, FirstOwner: stream);
        var closed = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, commandId),
            owner => AggregateOutcome.Commit(owner.Revoke(provenance, null, reason, actor, at, Placeholder, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(closed.IsSuccess, closed.Error?.Message);
    }

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
            expectedRevision ?? 0, new string('a', 64), at, stream, stream, FirstOwner: stream);
        var applied = await executor.ExecuteAsync(new ServiceEngagementIndependence(tenantId, engagementId, commandId),
            owner => AggregateOutcome.Commit(owner.Change(kind, provenance, change, Placeholder, result)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        return view;
    }
}
