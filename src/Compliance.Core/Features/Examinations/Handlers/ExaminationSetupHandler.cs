using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Versioning.Services;

namespace Bdgrz.Compliance.Features.Examinations.Handlers;

/// <summary>Invokes the examination owner after current actor and authoritative source admission.</summary>
public sealed class ExaminationSetupHandler(IAggregateReader reader, IAggregateExecutor executor,
    ExaminationSetupAccess access, IExaminationAttestEngagementReader attest, TimeProvider clock)
    : IRequestHandler<OpenExamination>, IRequestHandler<ChangeExaminationAuditor>,
        IRequestHandler<SetExaminationTargetDates>, IRequestHandler<ConfirmExaminationDates>,
        IRequestHandler<NameExaminationSigners>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<OpenExamination> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidScope(request.TenantId, request.ProgramId, request.ExaminationId, request.CommandId))
            return InvalidScope();
        var actor = await access.RequireAsync(context, request.TenantId, request.ProgramId,
            ExaminationSetupPermissions.Manage, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var existing = await reader.HydrateAsync(new Examination(request.TenantId, request.ExaminationId), ct).ConfigureAwait(false);
        if (!existing.HasAccepted(request.CommandId))
        {
            var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct).ConfigureAwait(false);
            if (!program.IsCreated)
                return Missing();
            var admission = await AdmitAuditorAsync(request.TenantId, request.Auditor, ct).ConfigureAwait(false);
            if (!admission.IsSuccess)
                return admission;
        }
        return await executor.ExecuteAsync(new Examination(request.TenantId, request.ExaminationId), owner =>
            CommandFailureRequestAdapter.ToOutcome(owner.Open(request, Uuid.Parse(actor.Value.Id), actor.Value.Display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result> HandleAsync(IRequestContext<ChangeExaminationAuditor> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidScope(request.TenantId, request.ProgramId, request.ExaminationId, request.CommandId))
            return InvalidScope();
        var actor = await access.RequireAsync(context, request.TenantId, request.ProgramId,
            ExaminationSetupPermissions.Manage, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var existing = await reader.HydrateAsync(new Examination(request.TenantId, request.ExaminationId), ct).ConfigureAwait(false);
        if (existing.Current?.ProgramId != request.ProgramId)
            return Missing();
        if (!existing.HasAccepted(request.CommandId))
        {
            var admission = await AdmitAuditorAsync(request.TenantId, request.Auditor, ct).ConfigureAwait(false);
            if (!admission.IsSuccess)
                return admission;
        }
        return await executor.ExecuteAsync(new Examination(request.TenantId, request.ExaminationId), owner =>
            CommandFailureRequestAdapter.ToOutcome(owner.ChangeAuditor(request, Uuid.Parse(actor.Value.Id), actor.Value.Display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result> HandleAsync(IRequestContext<SetExaminationTargetDates> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidScope(request.TenantId, request.ProgramId, request.ExaminationId, request.CommandId))
            return InvalidScope();
        var actor = await access.RequireAsync(context, request.TenantId, request.ProgramId,
            ExaminationSetupPermissions.Manage, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        return await executor.ExecuteAsync(new Examination(request.TenantId, request.ExaminationId), owner =>
            CommandFailureRequestAdapter.ToOutcome(owner.SetTargetDates(request, Uuid.Parse(actor.Value.Id), actor.Value.Display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result> HandleAsync(IRequestContext<ConfirmExaminationDates> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidScope(request.TenantId, request.ProgramId, request.ExaminationId, request.CommandId))
            return InvalidScope();
        var actor = await access.RequireAsync(context, request.TenantId, request.ProgramId,
            ExaminationSetupPermissions.Manage, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        return await executor.ExecuteAsync(new Examination(request.TenantId, request.ExaminationId), owner =>
            CommandFailureRequestAdapter.ToOutcome(owner.ConfirmDates(request, Uuid.Parse(actor.Value.Id), actor.Value.Display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result> HandleAsync(IRequestContext<NameExaminationSigners> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!ValidScope(request.TenantId, request.ProgramId, request.ExaminationId, request.CommandId))
            return InvalidScope();
        var actor = await access.RequireAsync(context, request.TenantId, request.ProgramId,
            RbacPermissions.TenantRbacManage, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var existing = await reader.HydrateAsync(new Examination(request.TenantId, request.ExaminationId), ct).ConfigureAwait(false);
        if (existing.Current?.ProgramId != request.ProgramId)
            return Missing();
        if (request.CeoSignerMemberId == Uuid.Empty || request.CisoSignerMemberId == Uuid.Empty)
            return InvalidScope();
        if (!existing.HasAccepted(request.CommandId))
        {
            foreach (var signer in new[] { request.CeoSignerMemberId, request.CisoSignerMemberId }.Distinct())
            {
                var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, signer), ct).ConfigureAwait(false);
                if (member.Affiliation != "client_personnel" ||
                    !member.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)))
                    return Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "Named examination signers must be current client members of this tenant."));
            }
        }
        return await executor.ExecuteAsync(new Examination(request.TenantId, request.ExaminationId), owner =>
            CommandFailureRequestAdapter.ToOutcome(owner.NameSigners(request, Uuid.Parse(actor.Value.Id), actor.Value.Display,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }

    ValueTask<Result> AdmitAuditorAsync(Uuid tenant, ExaminationAuditor? auditor, CancellationToken ct) =>
        auditor is { Kind: "attest", ServiceEngagementId: { } engagement }
            ? attest.RequireAcceptedOpenAttestAsync(tenant, engagement, ct) : ValueTask.FromResult(Result.Success);
    static bool ValidScope(Uuid tenant, Uuid program, Uuid examination, Uuid command) =>
        tenant != Uuid.Empty && program != Uuid.Empty && examination != Uuid.Empty && command != Uuid.Empty;
    static Result InvalidScope() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Examination setup requires exact tenant, program, examination and command identities."));
    static Result Missing() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The examination or program was not found in the requested scope."));
}
