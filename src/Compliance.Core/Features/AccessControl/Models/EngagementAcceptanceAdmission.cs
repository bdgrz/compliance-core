using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     Admits an engagement acceptance or a positive actual staff assignment as a root native fact of the engagement
///     owner. Admission compares the expected engagement revision with the owner, reads the exact service epoch, and
///     freezes the complete evidence, the paired personal decisions, every required source use and every practice
///     subject into the intent before the 202 acknowledgment is frozen on the root fact. An exact retry replays the
///     original acknowledgment even after the engagement, the service history or any source changed; nothing about
///     the operation is approved by admission alone.
/// </summary>
sealed class EngagementAcceptanceAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, IServiceEngagementAcceptanceEvidenceReader evidenceReader, FirmStaffViews staffViews,
    ProfessionalDutyAuthorityReader duties, PracticeIndependenceWall wall, EngagementSourcePins pins,
    IPlatformUserDirectoryReader platformUsers, TimeProvider clock, IDomainEventReader events)
{
    const string InitialEpisode = "initial";

    public ValueTask<Result<OperationAcceptedResponse>> AcceptAsync(IRequestContext<AcceptServiceEngagement> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Fail(RequestErrorKind.Forbidden,
                "Professional acceptance requires the assigned authenticated firm partner over personal HTTP.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return Fail(RequestErrorKind.Validation,
                "An acceptance requires a command, a client, an engagement and a non-negative expected revision.");
        var actor = ActorReference.ForFirmStaff(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return AdmitAsync(context, new Spec(request.TenantId, request.EngagementId, request.CommandId,
            ServiceEngagementIndependence.AcceptKind, EngagementIndependenceOperationKind.Acceptance,
            request.ExpectedRevision, actor, userId, AuthoredIntentDigest.Compute(request),
            (frozen, token) => BuildAcceptanceAsync(frozen, token)), ct);
    }

    public ValueTask<Result<OperationAcceptedResponse>> AssignAsync(
        IRequestContext<AssignServiceEngagementStaff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            throw new InvalidOperationException("ProfessionalEngagementPartnerAuthorizer must reject this actor.");
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.EngagementId == Uuid.Empty ||
            request.StaffMemberId == Uuid.Empty || request.ExpectedRevision < 0 || request.EffectiveFrom == default)
            return Fail(RequestErrorKind.Validation,
                "A staff assignment requires a command, a client, an engagement, a staff member, a non-negative expected revision and an effective date.");
        var actor = ActorReference.ForFirmStaff(userId, UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
        return AdmitAsync(context, new Spec(request.TenantId, request.EngagementId, request.CommandId,
            ServiceEngagementIndependence.AssignKind, EngagementIndependenceOperationKind.StaffAssignment,
            request.ExpectedRevision, actor, userId, AuthoredIntentDigest.Compute(request),
            (frozen, token) => BuildAssignmentAsync(frozen, request, token)), ct);
    }

    async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync(IExecutionContext context, Spec spec,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var realm = IndependenceContent.Realm(spec.TenantId);
        var stream = new ServiceEngagementIndependence(spec.TenantId, spec.EngagementId).Stream;
        var provenance = new OperationProvenance(spec.OperationId, spec.OperationId, null, spec.CommandKind, spec.Actor,
            stream, InitialEpisode, (ulong)spec.ExpectedRevision, spec.AuthoredDigest, now, stream, stream, FirstOwner: stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        ulong intentRevision = 0;
        var outcome = await operations.ExecuteAsync(new NativeOperation<ServiceEngagementIndependence>(binding,
            () => new ServiceEngagementIndependence(spec.TenantId, spec.EngagementId, spec.OperationId),
            owner => owner.Acceptance, async (owner, token) =>
            {
                var frozen = await FreezeAsync(spec, owner, provenance, now, realm, token).ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                intent = frozen.Value.Intent;
                intentRevision = owner.Revision;
                return Result<FirstResponseDescriptor>.Success(frozen.Value.Response);
            },
            (ServiceEngagementIndependence owner, ImmutableContentRef acknowledgment) =>
                owner.AdmitOperation(provenance, intent!, intentRevision, acknowledgment, spec.Kind)), context, ct).ConfigureAwait(false);
        return await IndependenceContent.ReadAcknowledgmentAsync(content, realm, outcome, ct).ConfigureAwait(false);
    }

    async ValueTask<Result<(ImmutableContentRef Intent, FirstResponseDescriptor Response)>> FreezeAsync(Spec spec,
        ServiceEngagementIndependence owner, OperationProvenance provenance, DateTimeOffset now, string realm,
        CancellationToken ct)
    {
        var prepared = owner.PrepareOperation(spec.OperationId, spec.Kind, spec.ExpectedRevision);
        if (!prepared.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(prepared.Error!);
        var clientService = await reader.HydrateAsync(new ClientServiceIndependence(spec.TenantId), ct)
            .ConfigureAwait(false);
        ServiceEngagementView view;
        IReadOnlyList<NonattestServiceView> services;
        try
        {
            view = await IndependenceContent.ReadResultAsync<ServiceEngagementView>(content, realm,
                owner.LatestResult!, ct).ConfigureAwait(false);
            services = await ClientServiceHistory.ReadAsync(events, content, clientService, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return IndependenceContent.Failure<(ImmutableContentRef, FirstResponseDescriptor)>(
                RequestErrorKind.Conflict, $"The engagement or service history cannot be verified: {exception.Message}",
                exception is ImmutableContentUnavailableException);
        }

        var completeServices = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            EngagementIndependenceIntent.ServiceItemSchema, services, SelectionOrder.Authored, ct)
            .ConfigureAwait(false);
        var history = new Frozen(spec, owner, view, clientService, services, now);
        var built = await spec.Build(history, ct).ConfigureAwait(false);
        if (!built.IsSuccess)
            return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Failure(built.Error!);
        var terms = built.Value!;

        // The service epoch is held like every other source, so a service recorded since admission rejects the operation.
        var serviceUse = new SourceUseRequirement(clientService.Stream,
            clientService.LatestService is { } latest
                ? NativeFactReference.Of(clientService.Stream, latest)
                : new NativeFactReference(clientService.Stream, Uuid.Empty, 0),
            clientService.Epoch, SourceUsePurpose.EngagementAcceptance, completeServices.Content);
        var requirements = terms.Uses.Append(serviceUse).GroupBy(use => use.SourceOwner).Select(group => group.First())
            .OrderBy(requirement => EngagementAcceptanceEffects.StreamKey(requirement.SourceOwner),
                StreamKeyOrder.Instance).ToArray();
        var subjects = terms.Subjects.GroupBy(subject => subject.Key).Select(group => group.First())
            .OrderBy(subject => subject.Key, StreamKeyOrder.Instance).ToArray();
        var required = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            EngagementIndependenceIntent.UseItemSchema, requirements, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var frozenSubjects = await content.WriteSelectionAsync(realm, Uuid.CreateVersion4(),
            EngagementIndependenceIntent.SubjectItemSchema, subjects, SelectionOrder.CanonicalKey, ct)
            .ConfigureAwait(false);
        var evidence = await IndependenceContent.WriteAsync(content, realm, EngagementIndependenceIntent.EvidenceSchema,
            terms.Evidence, ct).ConfigureAwait(false);

        var header = new DurableIntentHeader(spec.OperationId, provenance.Owner, provenance.Owner, provenance.Owner,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now);
        var intent = await IndependenceContent.WriteAsync(content, realm, EngagementIndependenceIntent.Schema,
            new EngagementIndependenceIntent(header, spec.EngagementId, (ulong)spec.ExpectedRevision, spec.Kind,
                clientService.Epoch, completeServices, evidence, terms.PartnerDecision, terms.Acknowledgement,
                frozenSubjects, required, terms.StaffMemberId, terms.EffectiveFrom), ct).ConfigureAwait(false);
        var acknowledgment = await IndependenceContent.WriteAsync(content, realm, "operation-accepted-response.v1",
            new OperationAcceptedResponse(spec.OperationId, EvaluateClientIndependenceHandler.StatusUrlTemplate
                .Replace("{tenant_id}", spec.TenantId.ToString(), StringComparison.Ordinal)
                .Replace("{operation_id}", spec.OperationId.ToString(), StringComparison.Ordinal), now), ct)
            .ConfigureAwait(false);
        return Result<(ImmutableContentRef, FirstResponseDescriptor)>.Success((intent,
            new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
                "operation-accepted-response.v1", new StandardResponseBody(acknowledgment))));
    }

    async ValueTask<Result<Terms>> BuildAcceptanceAsync(Frozen frozen, CancellationToken ct)
    {
        var (tenantId, engagementId) = (frozen.Spec.TenantId, frozen.Spec.EngagementId);
        var read = await evidenceReader.ReadCurrentAsync(tenantId, engagementId, frozen.Spec.UserId, ct)
            .ConfigureAwait(false);
        if (!read.IsSuccess)
            return Result<Terms>.Failure(read.Error!);
        if (read.Value is not { Proof: { } proof, RatifiedRules: { IsRatified: true, Version: > 0 } rules } ||
            proof.TenantId != tenantId || proof.EngagementId != engagementId ||
            proof.PartnerUserId != frozen.Spec.UserId || proof.ReviewedDraftRevision != frozen.Engagement.Revision ||
            proof.CurrentStaff is null || proof.CurrentPartner is not { IsActive: true })
            return Conflict("Current scoped engagement authority and ratified rules could not be verified; acceptance was not recorded.", true);
        var duty = await duties.ReadCurrentAsync(frozen.Spec.UserId, FirmProfessionalDuty.EngagementPartner, tenantId,
            ct).ConfigureAwait(false);
        if (duty is null)
            return Failure(RequestErrorKind.Forbidden, "The actor no longer has a current partner designation for this client.");
        if (duty.Staff != proof.CurrentPartner || proof.PartnerDutyRevision != duty.Designation.Revision ||
            proof.AuthorityReference != duty.Designation.SourceReference)
            return Failure(RequestErrorKind.Forbidden, "Only the currently designated partner may accept this engagement.");
        var current = await reader.HydrateAsync(new ClientServiceIndependence(tenantId), ct).ConfigureAwait(false);
        if (current.Epoch != frozen.ClientService.Epoch)
            return Conflict("A service was recorded while the acceptance was prepared; reload and retry.", true);

        var practice = frozen.Engagement.Content.Practice;
        var (decision, considered) = EngagementAcceptancePolicy.Evaluate(tenantId, practice, rules, frozen.Services,
            frozen.Engagement.Content.PeriodStart, proof.PartnerEvaluation is not null);
        if (decision.Code != IndependenceDecisionCode.Allowed)
            return Conflict("Independence policy refuses acceptance; impairment and unclassified services cannot be overridden.");
        if (await WallRefusalAsync(tenantId, engagementId, proof.CurrentPartner, practice, ct).ConfigureAwait(false)
                is { } partnerRefused)
            return Result<Terms>.Failure(partnerRefused);
        foreach (var staff in proof.CurrentStaff)
            if (await WallRefusalAsync(tenantId, engagementId, staff, practice, ct).ConfigureAwait(false) is { } refused)
                return Result<Terms>.Failure(refused);

        var uses = new List<SourceUseRequirement>();
        if (await pins.PartnerAsync(tenantId, frozen.Spec.UserId, duty, ct).ConfigureAwait(false) is not { } partnerPins ||
            await pins.RulesAsync(rules, ct).ConfigureAwait(false) is not { } rulePins)
            return Conflict("A partner or rule source changed while the acceptance was prepared; reload and retry.", true);
        uses.AddRange(partnerPins);
        uses.AddRange(rulePins);
        foreach (var staff in proof.CurrentStaff.Where(staff => staff.StaffMemberId != proof.CurrentPartner.StaffMemberId))
        {
            if (await pins.StaffAsync(staff.StaffMemberId, staff.Revision, ct).ConfigureAwait(false) is not { } pin)
                return Conflict("An assignee source changed while the acceptance was prepared; reload and retry.", true);
            uses.Add(pin);
        }

        var subjects = proof.CurrentStaff.SelectMany(staff => SubjectsOf(tenantId, staff, practice)).ToArray();
        var acknowledgement = await AcknowledgementFactAsync(tenantId, proof.ManagementAcknowledgementId, ct)
            .ConfigureAwait(false);
        NativeFactReference? evaluation = null;
        if (proof.PartnerEvaluation is { } recorded)
        {
            evaluation = await EvaluationFactAsync(tenantId, recorded.EvaluationId, ct).ConfigureAwait(false);
            if (evaluation is null)
                return Conflict("The partner evaluation is not materialized yet; retry after it is.", true);
        }

        if (acknowledgement is null)
            return Conflict("The management acknowledgement is not materialized yet; retry after it is.", true);
        var evidence = new EngagementAcceptanceEvidence(frozen.Engagement, frozen.Services, duty.Staff,
            duty.Designation, duty.ContinuityStartRevision, "allowed", EngagementAcceptanceViews.OutcomeOf(decision),
            considered, proof, rules, null);
        return Result<Terms>.Success(new Terms(evidence, evaluation, acknowledgement, uses, subjects, null, null));
    }

    async ValueTask<Result<Terms>> BuildAssignmentAsync(Frozen frozen, AssignServiceEngagementStaff request,
        CancellationToken ct)
    {
        var (tenantId, engagementId, now) = (frozen.Spec.TenantId, frozen.Spec.EngagementId, frozen.Now);
        if (!await platformUsers.ExistsAsync(frozen.Spec.UserId, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Forbidden,
                "A current partner, active client and canonical platform user are required for staff assignment.");
        if (frozen.Owner.AcceptanceResult is not { } result)
            return Conflict("Reload the active accepted engagement before assigning staff.", true);
        ServiceEngagementAcceptanceView acceptance;
        try
        {
            acceptance = await IndependenceContent.ReadResultAsync<ServiceEngagementAcceptanceView>(content,
                IndependenceContent.Realm(tenantId), result, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Conflict($"The accepted engagement cannot be verified: {exception.Message}", true);
        }

        if (acceptance.Status != "active")
            return Conflict("Reload the active accepted engagement before assigning staff.", true);
        var duty = await duties.ReadCurrentAsync(frozen.Spec.UserId, FirmProfessionalDuty.EngagementPartner, tenantId,
            ct).ConfigureAwait(false);
        if (duty is null)
            return Failure(RequestErrorKind.Forbidden, "The actor no longer has a current partner designation for this client.");
        var engagement = frozen.Engagement;
        if (await staffViews.GetAsync(request.StaffMemberId, ct).ConfigureAwait(false) is not { IsActive: true } staff ||
            staff.Practice != engagement.Content.Practice || !await platformUsers.ExistsAsync(staff.UserId, ct)
                .ConfigureAwait(false) || await staffViews.FindByUserAsync(staff.UserId, ct).ConfigureAwait(false) != staff)
            return Conflict("The requested assignment is not one active current firm-staff identity eligible for the engagement practice.", true);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (request.EffectiveFrom < today || request.EffectiveFrom < engagement.Content.PeriodStart ||
            engagement.Content.PeriodEnd is { } end && request.EffectiveFrom > end)
            return Failure(RequestErrorKind.Validation,
                "The assignment requires one current active practice-matched directory row and a non-backdated effective date inside the engagement period.");
        if (acceptance.Assignments.Any(item => item.StaffMemberId == staff.StaffMemberId || item.UserId == staff.UserId))
            return Conflict("A staff identity can be assigned to this accepted engagement only once; removed assignments remain permanent history.");
        if (await WallRefusalAsync(tenantId, engagementId, staff, engagement.Content.Practice, ct).ConfigureAwait(false)
                is { } refused)
            return Result<Terms>.Failure(refused);
        if (now < acceptance.RecordedAt || now < (acceptance.ChangedAt ?? acceptance.RecordedAt) ||
            frozen.Services.Any(service => service.RecordedAt > now))
            return Conflict("The complete client assignment history changed after the requested decision time.");

        var uses = new List<SourceUseRequirement>();
        if (await pins.PartnerAsync(tenantId, frozen.Spec.UserId, duty, ct).ConfigureAwait(false) is not { } partnerPins)
            return Conflict("A partner source changed while the assignment was prepared; reload and retry.", true);
        uses.AddRange(partnerPins);
        if (staff.StaffMemberId != duty.Staff.StaffMemberId)
        {
            if (await pins.StaffAsync(staff.StaffMemberId, staff.Revision, ct).ConfigureAwait(false) is not { } pin)
                return Conflict("The assignee source changed while the assignment was prepared; reload and retry.", true);
            uses.Add(pin);
        }

        var evidence = new EngagementAcceptanceEvidence(engagement, frozen.Services, duty.Staff, duty.Designation,
            duty.ContinuityStartRevision, acceptance.DecisionCode, acceptance.EvaluationOutcome,
            acceptance.ConsideredServiceRecordIds, null, null, staff);
        return Result<Terms>.Success(new Terms(evidence, null, null, uses,
            SubjectsOf(tenantId, staff, engagement.Content.Practice).ToArray(), staff.StaffMemberId,
            request.EffectiveFrom));
    }

    async ValueTask<RequestError?> WallRefusalAsync(Uuid tenantId, Uuid engagementId, FirmStaffMemberView staff,
        string practice, CancellationToken ct)
    {
        var decision = await wall.CanAssignAsync(tenantId, new EngagementAssignment(tenantId, engagementId,
            staff.StaffMemberId, staff.UserId, ServiceEngagementRules.PracticeOf(practice)), ct).ConfigureAwait(false);
        return decision.Code switch
        {
            IndependenceDecisionCode.Allowed => null,
            IndependenceDecisionCode.PersonPracticeConflict => new RequestError(RequestErrorKind.Conflict,
                $"Rule {decision.RuleId} refuses the opposite practice. Conflicting historical engagement: {decision.ConflictingAssignment!.EngagementId}; staff: {decision.ConflictingAssignment.FirmStaffMemberId}; user: {decision.ConflictingAssignment.UserId}."),
            _ => new RequestError(RequestErrorKind.Validation,
                "An assignment requires a valid engagement, practice, staff member and canonical user."),
        };
    }

    async ValueTask<NativeFactReference?> AcknowledgementFactAsync(Uuid tenantId, Uuid acknowledgementId,
        CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new EngagementManagementAcknowledgement(tenantId, acknowledgementId), ct)
            .ConfigureAwait(false);
        return owner.Acknowledged is { } fact ? NativeFactReference.Of(owner.Stream, fact) : null;
    }

    async ValueTask<NativeFactReference?> EvaluationFactAsync(Uuid tenantId, Uuid evaluationId, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new PartnerIndependenceDecision(tenantId, evaluationId), ct)
            .ConfigureAwait(false);
        return owner.Recorded is { } fact ? NativeFactReference.Of(owner.Stream, fact) : null;
    }

    static IEnumerable<PracticeSubjectIntent> SubjectsOf(Uuid tenantId, FirmStaffMemberView staff, string practice)
    {
        yield return new PracticeSubjectIntent(PracticeSubjectKind.Staff, staff.StaffMemberId, practice,
            PracticeSubjects.Stream(tenantId, PracticeSubjectKind.Staff, staff.StaffMemberId));
        yield return new PracticeSubjectIntent(PracticeSubjectKind.CanonicalUser, staff.UserId, practice,
            PracticeSubjects.Stream(tenantId, PracticeSubjectKind.CanonicalUser, staff.UserId));
    }

    static Result<Terms> Conflict(string message, bool transient = false) =>
        Result<Terms>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));

    static Result<Terms> Failure(RequestErrorKind kind, string message) =>
        Result<Terms>.Failure(new RequestError(kind, message));

    static ValueTask<Result<OperationAcceptedResponse>> Fail(RequestErrorKind kind, string message) =>
        ValueTask.FromResult(Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message)));

    sealed record Spec(Uuid TenantId, Uuid EngagementId, Uuid OperationId, string CommandKind,
        EngagementIndependenceOperationKind Kind, long ExpectedRevision, ActorReference Actor, Uuid UserId,
        string AuthoredDigest, Func<Frozen, CancellationToken, ValueTask<Result<Terms>>> Build);

    sealed record Frozen(Spec Spec, ServiceEngagementIndependence Owner, ServiceEngagementView Engagement,
        ClientServiceIndependence ClientService, IReadOnlyList<NonattestServiceView> Services, DateTimeOffset Now);

    sealed record Terms(EngagementAcceptanceEvidence Evidence, NativeFactReference? PartnerDecision,
        NativeFactReference? Acknowledgement, IReadOnlyList<SourceUseRequirement> Uses,
        IReadOnlyList<PracticeSubjectIntent> Subjects, Uuid? StaffMemberId, DateOnly? EffectiveFrom);
}
