using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>
///     The staff-record pair of the generic acquire receiver: it holds the exact registered, currently active staff
///     content a recording pinned at admission. A stale or inactive record rejects the change and fences the key.
/// </summary>
sealed class AuthorizeFirmStaffProfessionalDutyUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, ProfessionalDutyChangeVerifier verifier,
    ProfessionalDutyFences fences)
    : AuthorizeSourceUseHandler<FirmStaffRecord, AuthorizeFirmStaffProfessionalDutyUse>(reader, executor, content,
        clock)
{
    protected override FirmStaffRecord Open(AuthorizeFirmStaffProfessionalDutyUse request, Uuid selectedCommandId) =>
        new(request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizeFirmStaffProfessionalDutyUse request, CancellationToken ct)
    {
        var change = await verifier.VerifyAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (change.Error is not null)
            return Result<SourceUseSource>.Failure(change.Error);
        var (intent, owner) = (change.Intent!, change.Owner!);
        if (intent.StaffEligibility is not { } requirement ||
            Uuid.Parse(requirement.SourceOwner.Resource, CultureInfo.InvariantCulture) != request.SourceOwnerId ||
            requirement.ContentEvent.EventId != request.SourceContentEventId ||
            requirement.ResourceRevision != request.ExpectedContentRevision ||
            intent.DesignationId != request.DesignationId || request.Generation != ProfessionalDutyEffects.Generation)
            return Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The authorization differs from the frozen staff use requirement.", false));
        var superseded = owner.IsRejected(request.OperationId) ? "The change was rejected."
            : owner.IsApplied(request.OperationId) ? "The change already applied."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            ProfessionalDutyRules.StaffUseKey(request.OperationId, owner.Stream, requirement), requirement,
            nameof(AuthorizeFirmStaffProfessionalDutyUse), null, superseded)
        {
            RequiresAbort = owner.IsRejected(request.OperationId),
        });
    }

    protected override async ValueTask<Result> VerifyPinnedAsync(FirmStaffRecord owner,
        SourceUseRequirement requirement, CancellationToken ct)
    {
        if (!owner.IsRegistered)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The staff registration is not visible yet.", true));
        if (!IsPinned(owner, requirement))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The staff record changed after the change was accepted.", false));
        FirmStaffMemberView staff;
        try
        {
            staff = await FirmStaffContent.ReadStaffAsync(Content, requirement.RequiredFacts, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned staff content cannot be verified: {exception.Message}", false));
        }

        return staff.IsActive && staff.StaffMemberId == owner.StaffMemberId
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The staff member is not active; a designation requires active canonical staff.", false));
    }

    protected override bool IsPinned(FirmStaffRecord owner, SourceUseRequirement requirement) =>
        owner.Latest is { } latest && latest.Metadata.EventId == requirement.ContentEvent.EventId &&
        owner.Revision == requirement.ResourceRevision;

    protected override Func<FirmStaffRecord, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (FirmStaffRecord record, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            record.AuthorizeProfessionalDutyUse(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(AuthorizeFirmStaffProfessionalDutyUse request,
        SourceUseSource source, string reason, IExecutionContext context, CancellationToken ct)
    {
        var change = await verifier.VerifyAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        return change.Error is not null
            ? Result<ReceiverOutcome>.Failure(change.Error)
            : await fences.RejectAndFenceAsync(change, request.Source, request.Intent, reason, context, ct)
                .ConfigureAwait(false);
    }
}
