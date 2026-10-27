using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Exact scoped first-owner resolution and native preparation shared by the three assertion commands.</summary>
static class RiskTreatmentAssertionOperations
{
    public static async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(NativeOperationExecutor operations,
        IAggregateReader reader, RiskGovernanceReader history, IImmutableContentStore content, TimeProvider clock,
        IRequestContext<TRequest> context, Uuid tenant, Uuid program, Uuid risk, Uuid command, long expected,
        Uuid? control, Uuid treatment, string kind,
        Func<RiskTreatmentAssertion, OperationProvenance, CancellationToken, ValueTask<Result<PreparedRiskAssertion>>> prepare,
        Func<RiskTreatmentAssertion, OperationProvenance, ImmutableContentRef, PreparedRiskAssertion, Result> apply,
        CancellationToken ct) where TRequest : IRequestBase where TOut : notnull
    {
        if (tenant == Uuid.Empty || program == Uuid.Empty || risk == Uuid.Empty || command == Uuid.Empty || treatment == Uuid.Empty || expected < 0)
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation,
                "An assertion operation requires its typed identity and original non-negative revision."));
        var actor = RiskActor.From(context.Actor, tenant);
        try
        {
            EventStreamAddress? admission = null;
            if (control is null)
            {
                admission = await history.LocateAssertionAdmissionAsync(tenant, program, risk, command, ct).ConfigureAwait(false);
                if (admission is null)
                {
                    control = await history.LocateControlAsync(tenant, program, risk, treatment, ct).ConfigureAwait(false);
                    if (control is null)
                        return Result<TOut>.Failure(new RequestError(RequestErrorKind.NotFound, "The risk control treatment was not found."));
                }
            }
            RiskTreatmentAssertion Open() => admission is { } source
                ? RiskTreatmentAssertion.ForAcceptedReplay(tenant, program, risk, source, command)
                : new RiskTreatmentAssertion(tenant, program, risk, control!.Value, command);
            var ownerStream = Open().Stream;
            var operation = FindingContent.Provenance(command, kind, ownerStream,
                ActorReference.ForMember(actor.MemberId, actor.Display), (ulong)expected, FindingCommands.Digest(context.Request!),
                clock.GetUtcNow()) with
            { FirstOwner = ownerStream };
            PreparedRiskAssertion? prepared = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<RiskTreatmentAssertion>(AcceptedOperationBinding.Of(operation),
                Open, owner => owner.Acceptance,
                async (owner, token) =>
                {
                    if (admission is not null)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The scoped admission cannot be matched to its original native binding."));
                    var found = await RiskOwnerResolution.RequireRiskAsync(reader, tenant, program, risk, token).ConfigureAwait(false);
                    if (!found.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(found.Error!);
                    var next = await prepare(owner, operation, token).ConfigureAwait(false);
                    if (!next.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(next.Error!);
                    prepared = next.Value!;
                    return Result<FirstResponseDescriptor>.Success(prepared.Response);
                }, (owner, first) => apply(owner, operation, first, prepared!)), context, ct).ConfigureAwait(false);
            return await NativeContent.ResponseAsync<TOut>(content, tenant, accepted, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact scoped assertion source cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
