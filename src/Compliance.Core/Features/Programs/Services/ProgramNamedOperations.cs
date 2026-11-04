using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Authorizers;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Readiness.Events;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Programs.Services;

public sealed class ProgramNamedOperations(NativeOperationExecutor operations, IImmutableContentStore content,
    ISourceFactResolver sources, TimeProvider clock)
{
    public const string RegistrationSchema = "program-registration.v1";

    public ValueTask<Result<ProgramRegistration>> CreateAsync(IRequestContext<CreateProgramV3> context, CancellationToken ct) =>
        ExecuteAsync<CreateProgramV3, ProgramRegistration>(context, context.Request.ProgramId, nameof(CreateProgramV3), null,
            context.Request with { Name = context.Request.Name?.Trim()! }, ct);

    public async ValueTask<Result> RenameAsync(IRequestContext<RenameProgram> context, CancellationToken ct)
    {
        var request = context.Request with { Name = context.Request.Name?.Trim()! };
        var result = await ExecuteAsync<RenameProgram, UnitResult>(context, request.CommandId, nameof(RenameProgram),
            request.ExpectedRevision, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    public async ValueTask<Result> ReviseTargetsAsync(IRequestContext<ReviseProgramTargets> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = await ExecuteAsync<ReviseProgramTargets, UnitResult>(context, request.CommandId, nameof(ReviseProgramTargets),
            request.ExpectedRevision, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    public async ValueTask<Result> ChangeStageAsync(IRequestContext<ChangeProgramStage> context, CancellationToken ct)
    {
        var request = context.Request with { Rationale = context.Request.Rationale?.Trim()! };
        var result = await ExecuteAsync<ChangeProgramStage, UnitResult>(context, request.CommandId, nameof(ChangeProgramStage),
            request.ExpectedRevision, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
    }

    async ValueTask<Result<TOut>> ExecuteAsync<TRequest, TOut>(IRequestContext<TRequest> context, Uuid logicalId,
        string kind, long? expectedRevision, object intent, CancellationToken ct)
        where TRequest : IRequestBase where TOut : notnull
    {
        var requestScope = (Bdgrz.Compliance.Features.Programs.Contracts.IProgramLeadMutationRequest)intent;
        var tenant = requestScope.TenantId;
        var program = requestScope.ProgramId;
        if (tenant == Uuid.Empty || program == Uuid.Empty || logicalId == Uuid.Empty)
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, "A program command requires its authored identities."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        var owner = new ComplianceProgram(tenant, program);
        var operation = OwnerProvenance.For(owner, logicalId, kind,
            ActorReference.ForMember(RbacIds.Member(tenant, userId), UserIdentityClaims.BdgrzDisplay(context.Actor, userId)),
            RootOperation.InitialEpisode, expectedRevision is { } expected ? unchecked((ulong)expected) : null,
            IntentDigest(intent), clock.GetUtcNow()) with
        { FirstOwner = owner.Stream };
        NativePreparedCut? preparedCut = null;
        ImmutableContentRef? authored = null;
        ImmutableContentRef? result = null;
        ProgramContent? preceding = null;
        ProgramStageSupport? stageSupport = null;
        try
        {
            var accepted = await operations.ExecuteAsync(new NativeOperation<ComplianceProgram>(AcceptedOperationBinding.Of(operation),
                () => new ComplianceProgram(tenant, program, logicalId), current => current.Acceptance,
                async (current, token) =>
                {
                    preparedCut = NativePreparedCut.Of(current, current.NativeCut.Digest, current.NativeCut.MetadataDigest);
                    Result ready;
                    ProgramContent? value = null;
                    if (intent is CreateProgramV3 create)
                    {
                        ready = current.PrepareCreate();
                        if (create.EditionId == Uuid.Empty)
                            ready = Result.Failure(new RequestError(RequestErrorKind.Validation, "An exact criteria edition is required."));
                        value = new ProgramContent(create.Name, create.Plan);
                    }
                    else
                    {
                        ready = current.PrepareRevise(expectedRevision ?? 0);
                        if (ready.IsSuccess)
                        {
                            preceding = await ProgramReader.ReadContentAsync(content, tenant, current.CurrentContent!, token).ConfigureAwait(false);
                            if (intent is RenameProgram rename)
                                value = preceding with { Name = rename.Name };
                            else if (intent is ReviseProgramTargets targets)
                                value = preceding with { Plan = targets.Plan };
                            else if (intent is ChangeProgramStage stage)
                            {
                                stageSupport = await ReadStageSupportAsync(stage, token).ConfigureAwait(false);
                                var error = ProgramRules.StageError(tenant, program, current.Stage, stage.Stage, stageSupport, stage.Rationale);
                                if (error is not null)
                                    ready = Result.Failure(new RequestError(RequestErrorKind.Validation, error));
                                authored = current.CurrentContent;
                            }
                        }
                    }
                    if (!ready.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(ready.Error!);
                    if (value is not null)
                    {
                        var failure = ProgramRules.Validate(value);
                        if (failure is not null)
                            return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                        authored = preceding == value ? current.CurrentContent :
                            await NativeContent.WriteAsync(content, tenant, ProgramContent.Schema, value, token).ConfigureAwait(false);
                    }
                    var frozen = intent is CreateProgramV3
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
                        CreateProgramV3 creation => current.CreateWithEdition(operation, creation.EditionId, authored!, first, result!),
                        RenameProgram rename => current.Rename(operation, preceding!.Name, rename.Name, authored!, first, result!),
                        ReviseProgramTargets targets => current.ReviseTargets(operation, targets.Plan, authored!, first, result!),
                        ChangeProgramStage stage => current.ChangeStage(operation, stage.Stage, stageSupport!, stage.Rationale, first, result!),
                        _ => throw new InvalidOperationException("Another named program command."),
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
            var schema = intent is CreateProgramV3 ? RegistrationSchema : UnitResult.Schema;
            if (descriptor.HttpStatus != 200 || descriptor.ContentType != ImmutableContentStoreExtensions.JsonMediaType ||
                descriptor.ResultSchema != schema || descriptor.Body is not StandardResponseBody body || body.Content.Schema != schema)
                throw new ImmutableContentIntegrityException("The program original response has another status, schema or body kind.");
            var response = await content.ReadJsonAsync<TOut>(tenant.ToString(), body.Content, ct).ConfigureAwait(false);
            if (response is ProgramRegistration registration && registration.ProgramId != program)
                throw new ImmutableContentIntegrityException("The program original registration has another exact creation identity.");
            return Result<TOut>.Success(response);
        }
        catch (SourceFactVerificationException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, error.Message));
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<TOut>.Failure(ProgramReadFailure.Of(error));
        }
    }
    async Task<ProgramStageSupport> ReadStageSupportAsync(ChangeProgramStage request, CancellationToken ct)
    {
        if (request.SupportingReference is null)
            throw new SourceFactVerificationException("A stage decision requires a supporting committed source fact.");
        if (request.Stage == "type_i")
        {
            var (proof, fact, locator) = await SourceFactProofs.BuildAsync<TypeIEntryDecisionRecorded>(sources, content,
                request.TenantId.ToString(), request.SupportingReference, request.SupportingPosition, ct).ConfigureAwait(false);
            _ = proof;
            var decision = await content.ReadJsonAsync<TypeIEntryDecisionView>(request.TenantId.ToString(), fact.DecisionContent, ct).ConfigureAwait(false);
            if (fact.TenantId != request.TenantId || fact.ProgramId != request.ProgramId ||
                decision.DecisionId != fact.DecisionId || decision.AssessmentId != fact.AssessmentId ||
                decision.Outcome is not ("approve" or "approve_with_exceptions"))
                throw new SourceFactVerificationException("The stage support is not this program's approved Type I entry decision.");
            return new ProgramStageSupport("readiness", request.ProgramId, locator, fact.DecisionContent);
        }
        if (request.Stage == "type_ii")
        {
            var (proof, fact, locator) = await SourceFactProofs.BuildAsync<AuditOutcomeRecorded>(sources, content,
                request.TenantId.ToString(), request.SupportingReference, request.SupportingPosition, ct).ConfigureAwait(false);
            _ = proof;
            if (fact.TenantId != request.TenantId || fact.State is null || fact.State.TenantId != request.TenantId ||
                fact.State.ProgramId != request.ProgramId || fact.State.Content.ScopeKind != "type_i" || fact.State.Status != "recorded")
                throw new SourceFactVerificationException("The stage support is not this program's issued Type I examination outcome.");
            var snapshot = await NativeContent.WriteAsync(content, request.TenantId, "program-stage-support.v1", fact.State, ct).ConfigureAwait(false);
            return new ProgramStageSupport("examination", request.ProgramId, locator, snapshot);
        }
        throw new SourceFactVerificationException("A program may change only to its next stage.");
    }

    static string IntentDigest(object intent) => intent switch
    {
        CreateProgramV3 value => AuthoredIntentDigest.Compute(value),
        RenameProgram value => AuthoredIntentDigest.Compute(value),
        ReviseProgramTargets value => AuthoredIntentDigest.Compute(value),
        ChangeProgramStage value => AuthoredIntentDigest.Compute(value),
        _ => throw new InvalidOperationException("Another named program authored intent."),
    };
}
