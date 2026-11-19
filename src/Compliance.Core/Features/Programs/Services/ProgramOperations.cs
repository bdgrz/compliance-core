using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Authorizers;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Services;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Services;

public sealed class ProgramOperations(NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock,
    ICriteriaPublicationAuthority? publicationAuthority = null, IAccessAdmissionAuthority? admissionAuthority = null,
    IAggregateReader? creationReader = null, IAggregateExecutor? creationExecutor = null)
{
    public const string RegistrationSchema = "program-registration.v1";

    public ValueTask<Result<ProgramRegistration>> CreateAsync(IRequestContext<CreateProgram> context, CancellationToken ct) =>
        ExecuteAsync<CreateProgram, ProgramRegistration>(context, context.Request.ProgramId, nameof(CreateProgram), null,
            context.Request with { Name = context.Request.Name?.Trim()! }, ct);

    public ValueTask<Result<ProgramRegistration>> CreateAsync(IRequestContext<CreateProgramV3> context, CancellationToken ct) =>
        ExecuteAsync<CreateProgramV3, ProgramRegistration>(context, context.Request.ProgramId, nameof(CreateProgram), null,
            context.Request with { Name = context.Request.Name?.Trim()! }, ct);

    public async ValueTask<Result> ReviseAsync(IRequestContext<ReviseProgram> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<ReviseProgram, UnitResult>(context, request.CommandId, nameof(ReviseProgram), request.ExpectedRevision,
            request with { Name = request.Name?.Trim()! }, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    public async ValueTask<Result> SelectAsync(IRequestContext<SelectProgramCriteriaEdition> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<SelectProgramCriteriaEdition, UnitResult>(context, request.CommandId,
            nameof(SelectProgramCriteriaEdition), request.ExpectedRevision, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context, Uuid logicalId,
        string kind, long? expectedRevision, object intent, CancellationToken ct)
        where TRequest : IRequestBase where TOut : notnull
    {
        var tenant = intent switch
        {
            CreateProgram value => value.TenantId,
            CreateProgramV3 value => value.TenantId,
            ReviseProgram value => value.TenantId,
            SelectProgramCriteriaEdition value => value.TenantId,
            _ => throw new InvalidOperationException("Another program command.")
        };
        var program = intent switch
        {
            CreateProgram value => value.ProgramId,
            CreateProgramV3 value => value.ProgramId,
            ReviseProgram value => value.ProgramId,
            SelectProgramCriteriaEdition value => value.ProgramId,
            _ => throw new InvalidOperationException("Another program command.")
        };
        if (tenant == Uuid.Empty || program == Uuid.Empty || logicalId == Uuid.Empty)
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, "A program command requires its authored identities."));
        var creation = intent switch
        {
            CreateProgram value => value,
            CreateProgramV3 value => new CreateProgram(value.ProgramId, value.TenantId, value.Name, value.Plan, value.EditionId),
            _ => null,
        };
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var owner = new ComplianceProgram(tenant, program);
        ComplianceProgram? initialCreation = null;
        if (creation is not null)
        {
            if (creationReader is null || creationExecutor is null)
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Program creation fencing is unavailable.", true));
            try
            {
                initialCreation = await creationReader.HydrateAsync(new ComplianceProgram(tenant, program, logicalId), ct)
                    .ConfigureAwait(false);
            }
            catch (ImmutableContentIntegrityException error)
            {
                ct.ThrowIfCancellationRequested();
                return Result<TOut>.Failure(ProgramReadFailure.Of(error));
            }
        }
        AccessAdmissionEvidence? admission = null;
        if (creation is not null || intent is SelectProgramCriteriaEdition)
        {
            if (admissionAuthority is null)
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Source-authoritative program access is unavailable.", true));
            var authoredDigest = IntentDigest(intent);
            var digest = creation is not null
                ? ProgramAdmissionEvidence.CreationDigest(authoredDigest, initialCreation!.IsCreated
                    ? initialCreation.CreationGeneration : checked(initialCreation.CreationGeneration + 1))
                : authoredDigest;
            try
            {
                admission = creation is not null
                    ? await admissionAuthority.AdmitProgramCreationAsync(context, tenant, program,
                        owner.Stream.ToString(), digest, ct).ConfigureAwait(false)
                    : await admissionAuthority.AdmitProgramSelectionAsync(context, tenant, program, logicalId,
                        owner.Stream.ToString(), digest, ct).ConfigureAwait(false);
            }
            catch (AccessAdmissionUnavailableException)
            {
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Source-authoritative program access is unavailable.", true));
            }
            if (admission is null)
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Current program management authority is required."));
            if (!AccessAdmissionEvidenceBinding.IsCurrentFormat(admission))
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Source-authoritative program access is unavailable.", true));
            var identity = context.Actor.Identities.FirstOrDefault(candidate => candidate.IsAuthenticated &&
                string.Equals(candidate.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));
            var subject = identity?.FindFirst("sub")?.Value;
            var issuer = identity?.FindFirst("iss")?.Value;
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer) ||
                !AccessAdmissionEvidenceBinding.BindsProgramCreation(admission, userId,
                    new ActorAttribution(subject, issuer), logicalId, owner.Stream.ToString(), digest))
                return Result<TOut>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Program admission evidence does not match the authorized operation."));
        }
        var operation = OwnerProvenance.For(owner, logicalId, kind,
            ActorReference.ForMember(RbacIds.Member(tenant, userId), UserIdentityClaims.BdgrzDisplay(context.Actor, userId)),
            RootOperation.InitialEpisode, expectedRevision is { } expected ? unchecked((ulong)expected) : null,
            IntentDigest(intent), admission?.AdmittedAt ?? clock.GetUtcNow()) with
        { FirstOwner = owner.Stream };
        NativePreparedCut? preparedCut = null;
        ImmutableContentRef? authored = null;
        ImmutableContentRef? result = null;
        CriteriaPublicationProof? publication = null;
        var creationGeneration = initialCreation?.CreationGeneration ?? 0;
        try
        {
            if (creation is not null && initialCreation is { IsCreated: false })
            {
                var failure = ProgramRules.Validate(new ProgramContent(creation.Name, creation.Plan));
                if (failure is not null)
                    return Result<TOut>.Failure(failure.ToRequestError());
                var published = await ReadPublicationAsync(creation.EditionId, ct).ConfigureAwait(false);
                if (!published.IsSuccess)
                    return Result<TOut>.Failure(published.Error!);
                publication = published.Value;
                var position = initialCreation.CommittedStreamPosition;
                var generation = creationGeneration;
                try
                {
                    var fenced = await creationExecutor!.ExecuteAsync(new ComplianceProgram(tenant, program, logicalId), current =>
                    {
                        if (current.IsCreated)
                            return AggregateOutcome.Discard(Result.Success);
                        var applied = current.FenceCreationAttempt(position, generation, operation, admission!);
                        return applied.IsSuccess ? AggregateOutcome.Commit(applied) : AggregateOutcome.Discard(applied);
                    }, context, ct).ConfigureAwait(false);
                    if (!fenced.IsSuccess)
                        return Result<TOut>.Failure(fenced.Error!);
                    creationGeneration = checked(generation + 1);
                }
                catch (EventStreamConcurrencyException)
                {
                    return Result<TOut>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The creation source changed before the fence; reconcile and reauthorize.", true));
                }
            }
            var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceProgram>(AcceptedOperationBinding.Of(operation),
                () => new ComplianceProgram(tenant, program, logicalId), current => current.Acceptance,
                async (current, token) =>
                {
                    preparedCut = NativePreparedCut.Of(current, current.NativeCut.Digest, current.NativeCut.MetadataDigest);
                    Result ready;
                    ProgramContent? value = null;
                    if (creation is not null)
                    {
                        ready = current.PrepareCreate(creationGeneration, operation);
                        if (ready.IsSuccess && publication is null)
                        {
                            var published = await ReadPublicationAsync(creation.EditionId, token).ConfigureAwait(false);
                            if (!published.IsSuccess)
                                return Result<FirstResponseDescriptor>.Failure(published.Error!);
                            publication = published.Value;
                        }
                        value = new ProgramContent(creation.Name, creation.Plan);
                    }
                    else if (intent is ReviseProgram revise)
                    {
                        ready = current.PrepareRevise(revise.ExpectedRevision);
                        value = new ProgramContent(revise.Name, revise.Plan);
                    }
                    else
                    {
                        var selection = (SelectProgramCriteriaEdition)intent;
                        var decision = current.PrepareSelection(selection.ExpectedRevision, selection.EditionId);
                        ready = decision.IsSuccess ? Result.Success : Result.Failure(decision.Error!);
                        if (ready.IsSuccess)
                        {
                            var published = await ReadPublicationAsync(selection.EditionId, token).ConfigureAwait(false);
                            if (!published.IsSuccess)
                                return Result<FirstResponseDescriptor>.Failure(published.Error!);
                            publication = published.Value;
                        }
                    }
                    if (!ready.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                    if (value is not null)
                    {
                        var failure = ProgramRules.Validate(value);
                        if (failure is not null)
                            return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                        authored = await NativeContent.WriteAsync(content, tenant, ProgramContent.Schema, value, token).ConfigureAwait(false);
                    }
                    var frozen = creation is not null
                        ? await NativeContent.FreezeAsync(content, tenant, RegistrationSchema, new ProgramRegistration(program), token).ConfigureAwait(false)
                        : await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false);
                    result = frozen.Result;
                    return Result<FirstResponseDescriptor>.Success(frozen.Response);
                }, (current, first) =>
                {
                    if (preparedCut is null || preparedCut != NativePreparedCut.Of(current, current.NativeCut.Digest, current.NativeCut.MetadataDigest))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The exact program prefix changed during preparation; reload and retry.", true));
                    return intent switch
                    {
                        CreateProgram or CreateProgramV3 => current.Create(operation, authored!, first, result!, publication!, admission!, creationGeneration),
                        ReviseProgram => current.Revise(operation, authored!, first, result!),
                        SelectProgramCriteriaEdition selection => current.SelectCriteriaEdition(operation, selection.EditionId, first, result!, publication!, admission!),
                        _ => throw new InvalidOperationException("Another program command."),
                    };
                }), context, ct).ConfigureAwait(false);
            if (!accepted.IsSuccess)
                return Result<TOut>.Failure(accepted.Error!);
            var descriptor = accepted.Value switch
            {
                AcceptedAppendResult.Committed committed => committed.Response,
                AcceptedAppendResult.Replayed replayed => replayed.Response,
                _ => null,
            };
            if (descriptor is null)
                return Result<TOut>.Failure(AcceptedAppendResult.ConflictError);
            var schema = creation is not null ? RegistrationSchema : UnitResult.Schema;
            if (descriptor.HttpStatus != 200 || descriptor.ContentType != ImmutableContentStoreExtensions.JsonMediaType ||
                descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The program original response has another status, schema or body kind.");
            var response = await content.ReadJsonAsync<TOut>(tenant.ToString(), body.Content, ct).ConfigureAwait(false);
            if (response is ProgramRegistration registration && registration.ProgramId != program)
                throw new ImmutableContentIntegrityException("The program original registration has another exact creation identity.");
            return Result<TOut>.Success(response);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<TOut>.Failure(ProgramReadFailure.Of(error));
        }
    }

    async ValueTask<Result<CriteriaPublicationProof>> ReadPublicationAsync(Uuid editionId, CancellationToken ct)
    {
        if (editionId == Uuid.Empty)
            return Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Validation,
                "A published criteria edition is required."));
        if (publicationAuthority is null)
            return Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Conflict,
                "Criteria publication authority is unavailable.", true));
        var result = await publicationAuthority.ReadPublishedAsync(editionId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return Result<CriteriaPublicationProof>.Failure(result.Error!);
        var proof = result.Value;
        if (proof is null || proof.EditionId != editionId || proof.EventId == Uuid.Empty)
            return Result<CriteriaPublicationProof>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The criteria publication evidence does not match the requested edition."));
        return Result<CriteriaPublicationProof>.Success(proof);
    }
    static string IntentDigest(object intent) => intent switch
    {
        CreateProgram value => AuthoredIntentDigest.Compute(value),
        CreateProgramV3 value => AuthoredIntentDigest.Compute(new CreateProgram(value.ProgramId, value.TenantId,
            value.Name, value.Plan, value.EditionId)),
        ReviseProgram value => AuthoredIntentDigest.Compute(value),
        SelectProgramCriteriaEdition value => AuthoredIntentDigest.Compute(value),
        _ => throw new InvalidOperationException("Another program authored intent."),
    };

}
