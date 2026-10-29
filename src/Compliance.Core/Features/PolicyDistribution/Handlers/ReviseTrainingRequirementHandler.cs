using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     Defines an immutable successor version at the expected version, the sole logical concurrency counter. The
///     command id is the logical operation identity, so an exact retry replays the original registration even after
///     later revisions; launched campaigns keep the version they pinned.
/// </summary>
public sealed class ReviseTrainingRequirementHandler(NativeOperationExecutor operations,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviseTrainingRequirement, TrainingRequirementRegistration>
{
    const string CommandKind = "ReviseTrainingRequirement";

    public async ValueTask<Result<TrainingRequirementRegistration>> HandleAsync(
        IRequestContext<ReviseTrainingRequirement> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.RequirementId == Uuid.Empty || request.ExpectedVersion < 0)
            return Failure(RequestErrorKind.Validation,
                "A revision requires a command id, a requirement id and a non-negative expected version.");
        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new TrainingRequirement(request.TenantId, request.RequirementId).Stream;
        var provenance = PolicyCampaignContent.Provenance(request.CommandId, CommandKind, stream, stream,
            actor.Reference, request.ExpectedVersion, AuthoredIntentDigest.Compute(request), now) with
        { FirstOwner = stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? definition = null;
        ImmutableContentRef? result = null;
        string? definitionSha = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(TrainingRequirement requirement,
            CancellationToken token)
        {
            if (!requirement.IsDefined || requirement.ProgramId != request.ProgramId)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The training requirement was not found."));
            var prepared = requirement.PrepareRevise(request.ExpectedVersion);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (TrainingRequirementRules.Validate(request.Content) is { } error)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation, error));
            var clean = TrainingRequirementRules.Clean(request.Content);
            definitionSha = TrainingRequirementRules.Hash(clean);
            var registration = new TrainingRequirementRegistration(request.RequirementId, requirement.Identifier,
                requirement.CurrentVersion + 1);
            definition = await TrainingRequirementRules.WriteContentAsync(content, request.TenantId, clean, token)
                .ConfigureAwait(false);
            var body = await PolicyCampaignContent.WriteAsync(content, request.TenantId,
                PolicyCampaignContent.TrainingRegistrationSchema, registration, token).ConfigureAwait(false);
            result = await PolicyCampaignContent.WriteResultAsync(content, request.TenantId,
                PolicyCampaignContent.TrainingRegistrationSchema, body, token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(
                PolicyCampaignContent.OkResponse(PolicyCampaignContent.TrainingRegistrationSchema, body));
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<TrainingRequirement>(binding,
            () => new TrainingRequirement(request.TenantId, request.RequirementId, request.CommandId),
            requirement => requirement.Acceptance,
            PrepareAsync,
            (TrainingRequirement requirement, ImmutableContentRef frozen) => requirement.Revise(provenance,
                definition!, definitionSha!, actor.Reference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ResponseAsync<TrainingRequirementRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<TrainingRequirementRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<TrainingRequirementRegistration>.Failure(new RequestError(kind, message));
}
