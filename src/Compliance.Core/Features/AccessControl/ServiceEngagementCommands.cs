using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The shared flow of the engagement lifecycle commands. Each command is a root native fact on the engagement's own
///     owner: a retry resolves from the owner's facts before any guard or directory read, the complete resulting view
///     is frozen as the first response, and the append is checked against the engagement revision on every attempt.
///     The directory staff snapshot a command relied on is retained in the change, never re-read for a retry.
/// </summary>
sealed class ServiceEngagementCommands(NativeOperationExecutor operations,
    IImmutableContentStore content, FirmStaffViews staffViews, PracticeIndependenceWall wall, TimeProvider clock)
{
    const string InitialEpisode = "initial";
    const string StaffNotFound = "The current active firm-staff identity was not found.";

    public ValueTask<Result<ServiceEngagementView>> CreateAsync(IRequestContext<CreateServiceEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidIdentity(request.TenantId, request.EngagementId, request.EngagementId) ||
            !ServiceEngagementRules.ValidContent(request.Content))
            return Invalid(context.Request.Content is null
                ? "A service-engagement draft requires content."
                : "A draft engagement requires its valid period, practice, scope, current matching directory staff and client member attribution.");
        return RunAsync(context, request.TenantId, request.EngagementId, request.EngagementId,
            ServiceEngagementIndependence.CreateKind, request.ExpectedRevision, async (_, actor, at, token) =>
            {
                var lead = await ActiveStaffAsync(request.Content.EngagementLeadStaffMemberId, token)
                    .ConfigureAwait(false);
                if (lead is null)
                    return Missing();
                var created = ServiceEngagementRules.Create(request.TenantId, request.EngagementId, request.Content,
                    lead, actor, at);
                if (created.IsSuccess && await RefuseOnWallAsync(request.TenantId, request.EngagementId, lead, token)
                        .ConfigureAwait(false) is { } refused)
                    return refused;
                return created.IsSuccess
                    ? Accepted(created.Value!, ServiceEngagementIndependence.CreateKind, actor, at, request.Content,
                        lead, null, null)
                    : Result<Built>.Failure(created.Error!);
            }, ct);
    }

    public ValueTask<Result<ServiceEngagementView>> AmendAsync(IRequestContext<AmendServiceEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidIdentity(request.TenantId, request.EngagementId, request.CommandId) ||
            !ServiceEngagementRules.ValidContent(request.Content))
            return Invalid(request.Content is null
                ? "A service-engagement draft requires content."
                : "A draft engagement requires its valid period, practice, scope, current matching directory staff and client member attribution.");
        return RunAsync(context, request.TenantId, request.EngagementId, request.CommandId,
            ServiceEngagementIndependence.AmendKind, request.ExpectedRevision, async (current, actor, at, token) =>
            {
                var lead = await ActiveStaffAsync(request.Content.EngagementLeadStaffMemberId, token)
                    .ConfigureAwait(false);
                if (lead is null)
                    return Missing();
                var amended = ServiceEngagementRules.Amend(current!, request.Content, lead, actor, at);
                if (amended.IsSuccess && await RefuseOnWallAsync(request.TenantId, request.EngagementId, lead, token)
                        .ConfigureAwait(false) is { } refused)
                    return refused;
                return amended.IsSuccess
                    ? Accepted(amended.Value!, ServiceEngagementIndependence.AmendKind, actor, at, request.Content,
                        lead, null, null)
                    : Result<Built>.Failure(amended.Error!);
            }, ct);
    }

    public ValueTask<Result<ServiceEngagementView>> ProposeAsync(
        IRequestContext<ProposeServiceEngagementStaff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidIdentity(request.TenantId, request.EngagementId, request.CommandId) ||
            request.StaffMemberId == Uuid.Empty)
            return Invalid("A staff proposal requires an engagement, a command and a staff member.");
        return RunAsync(context, request.TenantId, request.EngagementId, request.CommandId,
            ServiceEngagementIndependence.ProposeKind, request.ExpectedRevision, async (current, actor, at, token) =>
            {
                var staff = await ActiveStaffAsync(request.StaffMemberId, token).ConfigureAwait(false);
                if (staff is null)
                    return Missing();
                var proposed = ServiceEngagementRules.Propose(current!, staff, actor, at);
                if (proposed.IsSuccess && await RefuseOnWallAsync(request.TenantId, request.EngagementId, staff, token)
                        .ConfigureAwait(false) is { } refused)
                    return refused;
                return proposed.IsSuccess
                    ? Accepted(proposed.Value!, ServiceEngagementIndependence.ProposeKind, actor, at, null, staff,
                        null, null)
                    : Result<Built>.Failure(proposed.Error!);
            }, ct);
    }

    public ValueTask<Result<ServiceEngagementView>> WithdrawAsync(
        IRequestContext<WithdrawServiceEngagementStaffProposal> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidIdentity(request.TenantId, request.EngagementId, request.CommandId) ||
            request.StaffMemberId == Uuid.Empty || !ServiceEngagementRules.Bounded(request.Reason))
            return Invalid("A proposal withdrawal requires an engagement, a command, a staff member and a bounded reason.");
        return RunAsync(context, request.TenantId, request.EngagementId, request.CommandId,
            ServiceEngagementIndependence.WithdrawKind, request.ExpectedRevision, (current, actor, at, _) =>
            {
                var withdrawn = ServiceEngagementRules.Withdraw(current!, request.StaffMemberId, actor, at);
                return ValueTask.FromResult(withdrawn.IsSuccess
                    ? Accepted(withdrawn.Value!, ServiceEngagementIndependence.WithdrawKind, actor, at, null, null,
                        request.StaffMemberId, request.Reason)
                    : Result<Built>.Failure(withdrawn.Error!));
            }, ct);
    }

    public ValueTask<Result<ServiceEngagementView>> CloseAsync(IRequestContext<CloseServiceEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidIdentity(request.TenantId, request.EngagementId, request.CommandId) ||
            !ServiceEngagementRules.Bounded(request.Reason))
            return Invalid("A closure requires an engagement, a command and a bounded reason.");
        return RunAsync(context, request.TenantId, request.EngagementId, request.CommandId,
            ServiceEngagementIndependence.CloseKind, request.ExpectedRevision, (current, actor, at, _) =>
                ValueTask.FromResult(Accepted(ServiceEngagementRules.Close(current!, actor, at),
                    ServiceEngagementIndependence.CloseKind, actor, at, null, null, null, request.Reason)), ct);
    }

    public async ValueTask<Result<ServiceEngagementAcceptanceView>> RevokeAsync(
        IRequestContext<RevokeServiceEngagementActualStaff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidIdentity(request.TenantId, request.EngagementId, request.CommandId) ||
            request.StaffMemberId == Uuid.Empty || !ServiceEngagementRules.Bounded(request.Reason))
            return IndependenceContent.Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Validation,
                "Actual-assignment revocation requires an attributed client decision.");
        if (request.ExpectedRevision < 0)
            return IndependenceContent.Failure<ServiceEngagementAcceptanceView>(RequestErrorKind.Validation,
                "The expected revision cannot be negative.");

        const string kind = "RevokeServiceEngagementActualStaff";
        var now = clock.GetUtcNow();
        var actor = AccessGrantActor.From(context, request.TenantId);
        var realm = IndependenceContent.Realm(request.TenantId);
        var stream = new ServiceEngagementIndependence(request.TenantId, request.EngagementId).Stream;
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, kind, actor, stream,
            InitialEpisode, (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now, stream,
            stream, FirstOwner: stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ServiceEngagementIndependence>(binding,
            () => new ServiceEngagementIndependence(request.TenantId, request.EngagementId, request.CommandId),
            owner => owner.Acceptance, async (owner, token) =>
            {
                var prepared = owner.PrepareRevocation(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                if (owner.AcceptanceResult is not { } acceptanceResult || owner.LatestResult is not { } draft)
                    return IndependenceContent.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict,
                        "The accepted engagement cannot be read yet; retry after it is available.", true);
                var acceptance = await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content, realm,
                    acceptanceResult, token).ConfigureAwait(false);
                var engagement = await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm, draft, token)
                    .ConfigureAwait(false);
                if (!acceptance.Assignments.Any(staff => staff.StaffMemberId == request.StaffMemberId && staff.IsCurrent))
                    return IndependenceContent.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict,
                        "Reload the current actual assignment before revoking.");
                var revised = ServiceEngagementRules.Revoke(acceptance, engagement.Content.EngagementLeadStaffMemberId,
                    request.StaffMemberId, request.Reason, actor, now);
                var revisedContent = await IndependenceContent.WriteAsync(content, realm, IndependenceContent.RevocationSchema,
                    revised, token).ConfigureAwait(false);
                result = await IndependenceContent.WriteResultAsync(content, realm, IndependenceContent.RevocationSchema,
                    revisedContent, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(IndependenceContent.Ok(IndependenceContent.RevocationSchema, revisedContent));
            }, (ServiceEngagementIndependence owner, ImmutableContentRef frozen) => owner.Revoke(provenance,
                request.StaffMemberId, request.Reason, actor, now, frozen, result!)), context, ct)
            .ConfigureAwait(false);
        return await IndependenceContent.ReadOutcomeAsync<ServiceEngagementAcceptanceView>(content, realm, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<ServiceEngagementView>> RunAsync<TRequest>(IRequestContext<TRequest> context, Uuid tenantId,
        Uuid engagementId, Uuid logicalId, string kind, long expectedRevision,
        Func<ServiceEngagementView?, ActorReference, DateTimeOffset, CancellationToken, ValueTask<Result<Built>>> build,
        CancellationToken ct) where TRequest : IRequestBase
    {
        if (expectedRevision < 0)
            return IndependenceContent.Failure<ServiceEngagementView>(RequestErrorKind.Validation,
                "The expected revision cannot be negative.");
        var now = clock.GetUtcNow();
        var actor = AccessGrantActor.From(context, tenantId);
        var realm = IndependenceContent.Realm(tenantId);
        var stream = new ServiceEngagementIndependence(tenantId, engagementId).Stream;
        var provenance = new OperationProvenance(logicalId, logicalId, null, kind, actor, stream, InitialEpisode,
            (ulong)expectedRevision, AuthoredIntentDigest.Compute(context.Request), now, stream, stream, FirstOwner: stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? change = null;
        ImmutableContentRef? result = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ServiceEngagementIndependence>(binding,
            () => new ServiceEngagementIndependence(tenantId, engagementId, logicalId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                var prepared = owner.PrepareChange(kind, expectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                ServiceEngagementView? current = null;
                if (owner.LatestResult is { } latest)
                {
                    try
                    {
                        current = await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm, latest, token)
                            .ConfigureAwait(false);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return IndependenceContent.Failure<FirstResponseDescriptor>(RequestErrorKind.Conflict,
                            $"The current engagement cannot be verified: {exception.Message}", exception is ImmutableContentUnavailableException);
                    }
                }

                var built = await build(current, actor, now, token).ConfigureAwait(false);
                if (!built.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(built.Error!);
                var view = await IndependenceContent.WriteAsync(content, realm, IndependenceContent.EngagementSchema,
                    built.Value!.View, token).ConfigureAwait(false);
                change = await IndependenceContent.WriteAsync(content, realm, IndependenceContent.EngagementChangeSchema,
                    built.Value.Change, token).ConfigureAwait(false);
                result = await IndependenceContent.WriteResultAsync(content, realm, IndependenceContent.EngagementSchema,
                    view, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(IndependenceContent.Ok(IndependenceContent.EngagementSchema, view));
            }, (ServiceEngagementIndependence owner, ImmutableContentRef frozen) =>
                owner.Change(kind, provenance, change!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await IndependenceContent.ReadOutcomeAsync<ServiceEngagementView>(content, realm, outcome, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     The permanent person-practice wall: a person who ever permanently served the opposite practice for this
    ///     client cannot even be proposed. The wall is read from the practice commitment owners, the same source the
    ///     management and compartment guards read.
    /// </summary>
    async ValueTask<Result<Built>?> RefuseOnWallAsync(Uuid tenantId, Uuid engagementId, FirmStaffMemberView staff,
        CancellationToken ct)
    {
        var decision = await wall.CanAssignAsync(tenantId, new EngagementAssignment(tenantId, engagementId,
            staff.StaffMemberId, staff.UserId, ServiceEngagementRules.PracticeOf(staff.Practice)), ct)
            .ConfigureAwait(false);
        return decision.Code switch
        {
            IndependenceDecisionCode.Allowed => null,
            IndependenceDecisionCode.PersonPracticeConflict => IndependenceContent.Failure<Built>(
                RequestErrorKind.Conflict,
                $"Rule {decision.RuleId} refuses the opposite practice. Conflicting historical engagement: {decision.ConflictingAssignment!.EngagementId}; staff: {decision.ConflictingAssignment.FirmStaffMemberId}; user: {decision.ConflictingAssignment.UserId}."),
            _ => IndependenceContent.Failure<Built>(RequestErrorKind.Validation,
                "A draft engagement requires its valid period, practice, scope, current matching directory staff and client member attribution."),
        };
    }

    async ValueTask<FirmStaffMemberView?> ActiveStaffAsync(Uuid staffMemberId, CancellationToken ct) =>
        await staffViews.GetAsync(staffMemberId, ct).ConfigureAwait(false) is { IsActive: true } staff ? staff : null;

    static bool ValidIdentity(Uuid tenantId, Uuid engagementId, Uuid logicalId) =>
        tenantId != Uuid.Empty && engagementId != Uuid.Empty && logicalId != Uuid.Empty;

    static ValueTask<Result<ServiceEngagementView>> Invalid(string message) =>
        ValueTask.FromResult(IndependenceContent.Failure<ServiceEngagementView>(RequestErrorKind.Validation, message));

    static Result<Built> Missing() => IndependenceContent.Failure<Built>(RequestErrorKind.NotFound, StaffNotFound);

    static Result<Built> Accepted(ServiceEngagementView view, string kind, ActorReference actor, DateTimeOffset at,
        ServiceEngagementDraftContent? content, FirmStaffMemberView? staff, Uuid? staffMemberId, string? reason) =>
        Result<Built>.Success(new Built(view, new ServiceEngagementChange(kind, content, staff,
            staffMemberId ?? staff?.StaffMemberId, reason, actor, at)));

    sealed record Built(ServiceEngagementView View, ServiceEngagementChange Change);
}
