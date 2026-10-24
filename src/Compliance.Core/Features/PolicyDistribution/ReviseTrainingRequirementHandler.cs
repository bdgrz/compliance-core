using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Defines an immutable successor version at the expected version, the sole logical concurrency counter. The
///     command id is the logical operation identity, so an exact retry replays the original registration even after
///     later revisions; launched campaigns keep the version they pinned.
/// </summary>
public sealed class ReviseTrainingRequirementHandler(NativeOperationExecutor operations, IAggregateReader reader,
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
            actor.Reference, request.ExpectedVersion, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var current = await reader.HydrateAsync(new TrainingRequirement(request.TenantId, request.RequirementId,
            request.CommandId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? definition = null;
        ImmutableContentRef? result = null;
        TrainingRequirementContent? clean = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (!current.IsDefined || current.ProgramId != request.ProgramId)
                return Failure(RequestErrorKind.NotFound, "The training requirement was not found.");
            var prepared = current.PrepareRevise(request.ExpectedVersion);
            if (!prepared.IsSuccess)
                return Result<TrainingRequirementRegistration>.Failure(prepared.Error!);
            if (TrainingRequirementRules.Validate(request.Content) is { } error)
                return Failure(RequestErrorKind.Validation, error);
            clean = TrainingRequirementRules.Clean(request.Content);
            var registration = new TrainingRequirementRegistration(request.RequirementId, current.Identifier,
                current.CurrentVersion + 1);
            definition = await TrainingRequirementRules.WriteContentAsync(content, request.TenantId, clean, ct)
                .ConfigureAwait(false);
            var body = await PolicyCampaignContent.WriteAsync(content, request.TenantId,
                PolicyCampaignContent.TrainingRegistrationSchema, registration, ct).ConfigureAwait(false);
            result = await PolicyCampaignContent.WriteResultAsync(content, request.TenantId,
                PolicyCampaignContent.TrainingRegistrationSchema, body, ct).ConfigureAwait(false);
            response = PolicyCampaignContent.OkResponse(PolicyCampaignContent.TrainingRegistrationSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<TrainingRequirement>(binding,
            () => new TrainingRequirement(request.TenantId, request.RequirementId, request.CommandId),
            requirement => requirement.Acceptance,
            requirement => Prepare(requirement, request.ExpectedVersion, response),
            (TrainingRequirement requirement, ImmutableContentRef frozen) => requirement.Revise(provenance,
                definition!, TrainingRequirementRules.Hash(clean!), actor.Reference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ResponseAsync<TrainingRequirementRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(TrainingRequirement requirement, long expectedVersion,
        FirstResponseDescriptor? response)
    {
        var prepared = requirement.PrepareRevise(expectedVersion);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The training requirement changed while the revision was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<TrainingRequirementRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<TrainingRequirementRegistration>.Failure(new RequestError(kind, message));
}
