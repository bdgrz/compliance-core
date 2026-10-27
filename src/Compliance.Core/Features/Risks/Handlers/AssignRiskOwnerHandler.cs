using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks.Handlers;

/// <summary>Resolves original acceptance before mutable risk/person selection and freezes one exact owner assignment.</summary>
public sealed class AssignRiskOwnerHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<AssignRiskOwner>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<AssignRiskOwner> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.RiskId == Uuid.Empty ||
            request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An owner assignment requires a command id and a non-negative expected revision."));
        var actor = RiskActor.From(context.Actor, request.TenantId);
        var stream = new RiskGovernance(request.TenantId, request.ProgramId, request.RiskId).Stream;
        var provenance = FindingContent.Provenance(request.CommandId, nameof(AssignRiskOwner), stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), (ulong)request.ExpectedRevision,
            FindingCommands.Digest(request), clock.GetUtcNow()) with
        { FirstOwner = stream };
        ImmutableContentRef? ownerContent = null;
        ImmutableContentRef? result = null;
        try
        {
            var outcome = await operations.ExecuteAsync(new NativeOperation<RiskGovernance>(AcceptedOperationBinding.Of(provenance),
                () => new RiskGovernance(request.TenantId, request.ProgramId, request.RiskId, request.CommandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        var gate = owner.PrepareAssignOwner(request.ExpectedRevision);
                        if (!gate.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(gate.Error!);
                        var risk = await RiskOwnerResolution.RequireRiskAsync(reader, request.TenantId, request.ProgramId, request.RiskId, token).ConfigureAwait(false);
                        if (!risk.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(risk.Error!);
                        if (request.PersonId == Uuid.Empty || RiskCommands.InvalidText(request.Rationale))
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                                "A risk owner requires a workforce person and a rationale of at most 4000 characters."));
                        var (exists, member) = await RiskOwnerResolution.ResolveAsync(reader, request.TenantId, request.PersonId, token).ConfigureAwait(false);
                        if (!exists)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                                "The risk owner must be a recorded workforce person in this organization."));
                        if (owner.Owner is { } current)
                        {
                            var existing = await FindingContent.ReadAsync<RiskOwnerView>(content, request.TenantId, current.OwnerContent, token).ConfigureAwait(false);
                            if (existing.PersonId == request.PersonId && existing.CorrelatedMemberId == member)
                                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The person already owns the risk."));
                        }
                        ownerContent = await FindingContent.WriteAsync(content, request.TenantId, RiskContent.OwnerSchema,
                            new RiskOwnerView(request.PersonId, member, request.Rationale.Trim(), provenance.Actor, provenance.AcceptedAt), token).ConfigureAwait(false);
                        var frozen = await RiskCommands.FreezeUnitAsync(content, request.TenantId, token).ConfigureAwait(false);
                        result = frozen.Result;
                        return Result<FirstResponseDescriptor>.Success(frozen.Response);
                    }
                    catch (ImmutableContentIntegrityException error)
                    {
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact risk owner source cannot be verified.", error is ImmutableContentUnavailableException));
                    }
                }, (owner, first) => owner.AssignOwner(provenance, ownerContent!, first, result!)), context, ct).ConfigureAwait(false);
            return await RiskCommands.UnitAsync(content, request.TenantId, outcome, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact risk owner content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
