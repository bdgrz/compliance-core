using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
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
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Removes the exact team assignment directly. Team membership is independent of a primary designation.</summary>
public sealed class RemoveTeamMemberHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RemoveTeamMember, AdministratorTransitionResponse>
{
    const string CommandKind = "RemoveTeamMember";
    const string CurrentEpisode = "current";

    public async ValueTask<Result<AdministratorTransitionResponse>> HandleAsync(
        IRequestContext<RemoveTeamMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var isSystem = RequestActor.IsSystem(context.Actor);
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.TeamId == Uuid.Empty ||
            request.MemberId == Uuid.Empty || (request.MemberEpisodeId == Uuid.Empty && !isSystem))
            return Fail(RequestErrorKind.Validation, "A command id, tenant, team, member and member episode are required.");
        if ((request.ExpectedAssignmentRevision < 0 || request.ExpectedAssignmentGeneration < 0) && !isSystem)
            return Fail(RequestErrorKind.Validation, "The expected assignment revision and generation must not be negative.");
        var actor = RegisterMemberHandler.ActorOf(context.Actor);
        if (actor is null)
            return Fail(RequestErrorKind.Forbidden, "The caller identity is not recognized.");

        var provenance = Provenance(request, actor, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        var accepted = await operations.ExecuteAsync(new NativeOperation<TeamMember>(binding,
            () => Owner(request, request.CommandId), owner => owner.Acceptance,
            (owner, token) => PrepareAsync(owner, request, provenance.AcceptedAt, token),
            (TeamMember owner, ImmutableContentRef response) => owner.Remove(provenance, response, request.MemberEpisodeId,
                request.ExpectedAssignmentRevision, request.ExpectedAssignmentGeneration)), context, ct)
            .ConfigureAwait(false);
        return await TeamMemberResponses.ReadAsync(content, request.TenantId.ToString(), accepted, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(TeamMember owner, RemoveTeamMember request,
        DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var prepared = owner.PrepareRemove(request.MemberEpisodeId, request.ExpectedAssignmentRevision,
            request.ExpectedAssignmentGeneration);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return Result<FirstResponseDescriptor>.Success(await TeamMemberResponses.FreezeCompletedAsync(content,
            request.TenantId.ToString(), request.CommandId, acceptedAt, ct).ConfigureAwait(false));
    }

    /// <summary>The provenance a removal carries; the binding of a retry is derived from it.</summary>
    internal static OperationProvenance Provenance(RemoveTeamMember request, ActorReference actor,
        DateTimeOffset acceptedAt) =>
        Owner(request, Uuid.Empty).Provenance(request.CommandId, CommandKind, actor,
            request.MemberEpisodeId == Uuid.Empty ? CurrentEpisode : request.MemberEpisodeId.ToString(),
            request.ExpectedAssignmentRevision >= 0 ? (ulong)request.ExpectedAssignmentRevision : null,
            AuthoredIntentDigest.Compute(request), acceptedAt);

    static TeamMember Owner(RemoveTeamMember request, Uuid selectedCommandId) =>
        new(request.TenantId, request.TeamId, request.MemberId, selectedCommandId);

    static Result<AdministratorTransitionResponse> Fail(RequestErrorKind kind, string message) =>
        Result<AdministratorTransitionResponse>.Failure(new RequestError(kind, message));
}
