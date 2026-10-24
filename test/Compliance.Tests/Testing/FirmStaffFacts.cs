using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using System.Security.Claims;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds well-formed staff record facts straight onto the owners, exactly as the registration process and the
///     status handler leave them: accepted, claimed, completed, then status changes at consecutive revisions.
/// </summary>
static class FirmStaffFacts
{
    static readonly DateTimeOffset At = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    static readonly ImmutableContentRef FirstResponse = new(Uuid.Parse("00000000-0000-4000-8000-0000000000f1",
        System.Globalization.CultureInfo.InvariantCulture), new string('b', 64), 2, "application/json",
        "first-response-descriptor.v1");

    /// <summary>Registers a staff member: accepted registration, permanent claim and completion at revision one.</summary>
    public static async Task<FirmStaffMemberView> RegisterAsync(IServiceProvider provider, Uuid staffMemberId,
        Uuid userId, string practice = "attest", string source = "Synthetic staff source",
        ActorReference? actor = null, DateTimeOffset? at = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var recordedAt = at ?? At;
        var operatorActor = actor ?? ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
        var record = new FirmStaffRecord(staffMemberId, staffMemberId);
        var claim = new FirmStaffIdentityClaim(userId);
        var header = new DurableIntentHeader(staffMemberId, record.Stream, record.Stream, record.Stream,
            operatorActor, "initial", 0, new string('a', 64), recordedAt);
        var intent = new FirmStaffRegistrationIntent(header, staffMemberId, userId, practice, source,
            FirmStaffRegistrationIntent.ActiveStatus);
        var intentRef = await content.WriteJsonAsync(FirmStaffContentRealm, Uuid.CreateVersion4(),
            FirmStaffRegistrationIntent.Schema, intent);
        var accepted = await executor.ExecuteAsync(record, item => AggregateOutcome.Commit(item.AcceptRegistration(
            Provenance(staffMemberId, staffMemberId, "RegisterFirmStaff", operatorActor, record.Stream, header,
                recordedAt), intentRef, FirstResponse)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var claimId = Uuid.CreateVersion4();
        var claimed = await executor.ExecuteAsync(claim, item => AggregateOutcome.Commit(item.Claim(
            Provenance(claimId, staffMemberId, "ClaimFirmStaffIdentity", operatorActor, claim.Stream, header,
                recordedAt), intentRef)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        var view = new FirmStaffMemberView(staffMemberId, userId, practice, source, true, 1, operatorActor,
            recordedAt);
        var staffRef = await content.WriteJsonAsync(FirmStaffContentRealm, Uuid.CreateVersion4(),
            "FirmStaffChangeRecorded.StaffContent.v1", view);
        var resultRef = await content.WriteJsonAsync(FirmStaffContentRealm, Uuid.CreateVersion4(),
            "mutation-result-content.v1", new MutationResultContent("FirmStaffChangeRecorded.StaffContent.v1",
                new StandardResponseBody(staffRef)));
        var completed = await executor.ExecuteAsync(new FirmStaffRecord(staffMemberId, Uuid.Empty), item =>
            AggregateOutcome.Commit(item.CompleteRegistration(Provenance(Uuid.CreateVersion4(), staffMemberId,
                "CompleteFirmStaffRegistration", operatorActor, record.Stream, header, recordedAt), staffRef,
                resultRef)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        return view;
    }

    /// <summary>Changes the status of a registered staff member at its current revision.</summary>
    public static async Task<FirmStaffMemberView> SetStatusAsync(IServiceProvider provider, Uuid staffMemberId,
        bool isActive, string reason = "Synthetic status change", DateTimeOffset? at = null,
        ActorReference? actor = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var views = scope.ServiceProvider.GetRequiredService<FirmStaffViews>();
        var current = await views.GetAsync(staffMemberId) ?? throw new InvalidOperationException("Register first.");
        actor ??= ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
        var recordedAt = at ?? current.RecordedAt.AddMinutes(1);
        var next = current with { IsActive = isActive, Revision = current.Revision + 1, Actor = actor, RecordedAt = recordedAt };
        var staffRef = await content.WriteJsonAsync(FirmStaffContentRealm, Uuid.CreateVersion4(),
            "FirmStaffChangeRecorded.StaffContent.v1", next);
        var resultRef = await content.WriteJsonAsync(FirmStaffContentRealm, Uuid.CreateVersion4(),
            "mutation-result-content.v1", new MutationResultContent("FirmStaffChangeRecorded.StaffContent.v1",
                new StandardResponseBody(staffRef)));
        var commandId = Uuid.CreateVersion4();
        var stream = new FirmStaffRecord(staffMemberId).Stream;
        var provenance = new OperationProvenance(commandId, commandId, null, "SetFirmStaffStatus", actor, stream,
            "initial", (ulong)current.Revision, new string('c', 64), recordedAt, stream, stream);
        var changed = await executor.ExecuteAsync(new FirmStaffRecord(staffMemberId, commandId), item =>
            AggregateOutcome.Commit(item.SetStatus(provenance, reason, staffRef, FirstResponse, resultRef)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(changed.IsSuccess, changed.Error?.Message);
        return next;
    }

    /// <summary>
    ///     Admits <paramref name="request" /> through the bus and runs its registration process to the end, as the
    ///     reactor and sweep do in production. Returns the original 202 admission result.
    /// </summary>
    public static async Task<Result<OperationAcceptedResponse>> RegisterThroughProcessAsync(
        IServiceProvider provider, ClaimsPrincipal actor, RegisterFirmStaff request)
    {
        await using var scope = provider.CreateAsyncScope();
        var admitted = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(request, actor);
        if (admitted.IsSuccess)
            await RunProcessAsync(provider, request.StaffMemberId);
        return admitted;
    }

    /// <summary>Projects every staff change fact into the directory read model, as the hosted projector does.</summary>
    public static async Task ProjectDirectoryAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var projector = new FirmStaffDirectoryProjector(scope.ServiceProvider.GetRequiredService<FitzFirmStaffDirectory>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        await new ProjectorRunner(scope.ServiceProvider.GetRequiredService<IDomainEventReader>())
            .RunAsync(projector, ProjectionCheckpoint.Start);
    }

    /// <summary>Starts (if absent) and drives the registration process of an accepted registration.</summary>
    public static async Task<ProcessRecord> RunProcessAsync(IServiceProvider provider, Uuid staffMemberId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var record = await reader.HydrateAsync(new FirmStaffRecord(staffMemberId));
        var accepted = record.Accepted ?? throw new InvalidOperationException("The registration was not accepted.");
        var intent = await services.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<FirmStaffRegistrationIntent>(FirmStaffContentRealm, accepted.Intent);
        var key = new ProcessKey(FirmStaffEffects.ProcessName, FirmStaffContentRealm, staffMemberId);
        await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(ProcessRecord.Start(key,
            NativeFactReference.Of(record.Stream, accepted), accepted.Operation.Episode, accepted.Intent,
            [intent.CanonicalUserId.ToString()], DateTimeOffset.UtcNow));
        return await services.GetRequiredService<ProcessDriver>().RunAsync(key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    const string FirmStaffContentRealm = "bdgrz";

    static OperationProvenance Provenance(Uuid logicalId, Uuid rootId, string kind, ActorReference actor,
        EventStreamAddress owner, DurableIntentHeader header, DateTimeOffset acceptedAt) => new(logicalId, rootId,
        logicalId == rootId ? null : rootId, kind, actor, owner, header.Episode, header.OriginalExpectedRevision,
        header.AuthoredIntentSha256, acceptedAt, owner, owner);
}
