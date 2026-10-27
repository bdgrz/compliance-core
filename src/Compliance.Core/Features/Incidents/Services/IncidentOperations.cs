using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.Incidents.Persistence;
using Bdgrz.Compliance.Features.Incidents.Projections;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Incidents.Aggregates;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Incidents.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Incidents.Services;

/// <summary>Exact native acceptance; source guards and immutable publication stay inside recovery-aware preparation.</summary>
public sealed class IncidentOperations(NativeOperationExecutor operations, ComplianceIncidentReader incidents,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result> ExecuteAsync<TRequest>(IRequestContext<TRequest> context, Uuid tenant, Uuid program,
        Uuid incident, Uuid command, string kind, long? expected, ActorReference actor,
        Func<ComplianceIncidentReadSnapshot, OperationProvenance, CancellationToken, ValueTask<Result>> prepare,
        CancellationToken ct) where TRequest : IRequestBase
    {
        if (tenant == Uuid.Empty || program == Uuid.Empty || incident == Uuid.Empty || command == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "An incident operation requires exact identities and its positive original revision when required."));
        if (expected is < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The expected revision must not be negative."));
        var target = new ComplianceIncident(tenant, program, incident);
        var operation = OwnerProvenance.For(target, command, kind,
            actor, incident.ToString(), expected is { } revision ? unchecked((ulong)revision) : null,
            AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow()) with
        { FirstOwner = target.Stream };
        FrozenComplianceIncidentMutation? frozen = null;
        ImmutableContentRef? result = null;
        var sequence = 0L;
        var position = 0UL;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceIncident>(AcceptedOperationBinding.Of(operation),
                () => new ComplianceIncident(tenant, program, incident, command), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    try
                    {
                        if (kind == nameof(RecordComplianceIncident) ? owner.Header is not null : owner.Header is null || owner.ProgramId != program)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(kind == nameof(RecordComplianceIncident) ? RequestErrorKind.Conflict : RequestErrorKind.NotFound,
                                "The incident is not available for this operation."));
                        if (expected is { } original && owner.Revision != original)
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The incident requires its original expected logical revision."));
                        var snapshot = await incidents.ReadCurrentAsync(owner, program, token).ConfigureAwait(false);
                        var before = snapshot.View;
                        var ready = await prepare(snapshot, operation, token).ConfigureAwait(false);
                        if (!ready.IsSuccess)
                            return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                        frozen = await ComplianceIncidentContentStore.FreezeAsync(content, before, snapshot.Authority, owner.Header, snapshot.Rules, kind, token).ConfigureAwait(false);
                        var response = await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false);
                        result = response.Result;
                        sequence = owner.NativeSequence;
                        position = owner.CommittedStreamPosition;
                        return Result<FirstResponseDescriptor>.Success(response.Response);
                    }
                    catch (ImmutableContentIntegrityException exception)
                    {
                        return Result<FirstResponseDescriptor>.Failure(ContentFailure(exception));
                    }
                }, (owner, response) => owner.Append(ComplianceIncidentContentStore.Fact(frozen!, sequence + 1, operation, response, result!),
                    frozen!, sequence, position)), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result.Failure(AcceptedAppendResult.ConflictError);
            if (descriptor.HttpStatus != 200 || descriptor.ResultSchema != UnitResult.Schema ||
                descriptor.Body is not StandardResponseBody body || body.Content.Schema != UnitResult.Schema)
                throw new ImmutableContentIntegrityException("The original incident response has another result contract.");
            await content.ReadJsonAsync<UnitResult>(tenant.ToString(), body.Content, ct).ConfigureAwait(false);
            return Result.Success;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(ContentFailure(exception));
        }
    }
    public static RequestError ContentFailure(ImmutableContentIntegrityException exception) => new(RequestErrorKind.Conflict,
        "The incident's exact immutable source cannot be verified.", exception is ImmutableContentUnavailableException);
    public static Result Outcome(CommandFailure? failure) => failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
}
