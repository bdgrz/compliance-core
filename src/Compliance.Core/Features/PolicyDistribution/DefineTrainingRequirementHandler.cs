using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Defines the first version of a training requirement. The supplied requirement id is the logical operation
///     identity and must equal the deterministic id of the tenant, program and normalized identifier, so an exact retry
///     replays the original registration even after later revisions. Invalid content never appends.
/// </summary>
public sealed class DefineTrainingRequirementHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<DefineTrainingRequirement, TrainingRequirementRegistration>
{
    const string CommandKind = "DefineTrainingRequirement";

    public async ValueTask<Result<TrainingRequirementRegistration>> HandleAsync(
        IRequestContext<DefineTrainingRequirement> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.RequirementId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A training requirement requires its tenant, program and requirement id.");
        var normalized = TrainingRequirementIdentity.NormalizeIdentifier(request.Identifier);
        if (!TrainingRequirementIdentity.IsValidIdentifier(normalized))
            return Failure(RequestErrorKind.Validation,
                "A training requirement identifier requires 1 to 80 ASCII letters, digits, hyphens, underscores, or periods.");
        if (TrainingRequirementIdentity.IdFor(request.TenantId, request.ProgramId, normalized) != request.RequirementId)
            return Failure(RequestErrorKind.Validation,
                "The requirement id must equal the identity of the tenant, program and normalized identifier.");

        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new TrainingRequirement(request.TenantId, request.RequirementId).Stream;
        var provenance = PolicyCampaignContent.Provenance(request.RequirementId, CommandKind, stream, stream,
            actor.Reference, null, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);
        var current = await reader.HydrateAsync(new TrainingRequirement(request.TenantId, request.RequirementId,
            request.RequirementId), ct).ConfigureAwait(false);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? definition = null;
        ImmutableContentRef? result = null;
        TrainingRequirementContent? clean = null;
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            if (TrainingRequirementRules.Validate(request.Content) is { } error)
                return Failure(RequestErrorKind.Validation, error);
            clean = TrainingRequirementRules.Clean(request.Content);
            var registration = new TrainingRequirementRegistration(request.RequirementId, normalized, 1);
            definition = await TrainingRequirementRules.WriteContentAsync(content, request.TenantId, clean, ct)
                .ConfigureAwait(false);
            var body = await PolicyCampaignContent.WriteAsync(content, request.TenantId,
                PolicyCampaignContent.TrainingRegistrationSchema, registration, ct).ConfigureAwait(false);
            result = await PolicyCampaignContent.WriteResultAsync(content, request.TenantId,
                PolicyCampaignContent.TrainingRegistrationSchema, body, ct).ConfigureAwait(false);
            response = PolicyCampaignContent.OkResponse(PolicyCampaignContent.TrainingRegistrationSchema, body);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<TrainingRequirement>(binding,
            () => new TrainingRequirement(request.TenantId, request.RequirementId, request.RequirementId),
            requirement => requirement.Acceptance, requirement => Prepare(requirement, response),
            (TrainingRequirement requirement, ImmutableContentRef frozen) => requirement.Define(provenance,
                request.ProgramId, normalized, definition!, TrainingRequirementRules.Hash(clean!), actor.Reference, now,
                frozen, result!)), context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.ResponseAsync<TrainingRequirementRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<FirstResponseDescriptor> Prepare(TrainingRequirement requirement, FirstResponseDescriptor? response)
    {
        var prepared = requirement.PrepareDefine();
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The training requirement changed while the definition was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<TrainingRequirementRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<TrainingRequirementRegistration>.Failure(new RequestError(kind, message));
}
