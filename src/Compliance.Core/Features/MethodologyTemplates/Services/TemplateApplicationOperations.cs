using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Services;

/// <summary>Resolves exact native retries before source reads, then freezes one business delta and original result under owner OCC.</summary>
public sealed class TemplateApplicationOperations(IAggregateReader reader, TemplateApplicationReader applications,
    NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock)
{
    public IAggregateReader Sources => reader;

    public async ValueTask<Result> ExecuteAsync<T>(IRequestContext<T> context, Uuid tenant, Uuid program,
        Uuid template, string applicationKey, Uuid commandId, string kind, long? expectedRevision,
        Func<TemplateApplicationRules, Uuid, string, DateTimeOffset, CancellationToken, ValueTask<CommandFailure?>> prepare,
        CancellationToken ct) where T : IRequestBase
    {
        if (tenant == Uuid.Empty || program == Uuid.Empty || template == Uuid.Empty || commandId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(applicationKey) || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A template application requires exact scoped identities and an authorized personal actor."));
        var actor = ClientServiceActor.Snapshot(context);
        var applicationId = TemplateApplicationId.For(program, template, applicationKey);
        var operation = OwnerProvenance.For(new TemplateApplication(tenant, program, applicationId), commandId, kind,
            ActorReference.ForMember(actor.MemberId, actor.Display), applicationId.ToString(),
            expectedRevision is { } expected ? unchecked((ulong)expected) : null,
            IntentDigest(context.Request), clock.GetUtcNow());
        try
        {
            long revision = 0;
            ulong physical = 0;
            TemplateApplicationView? before = null;
            TemplateApplicationView? after = null;
            TemplateApplicationHeader? header = null;
            TemplateApplicationMutationContent? mutation = null;
            ImmutableContentRef? change = null;
            ImmutableContentRef? outcome = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<TemplateApplication>(
                AcceptedOperationBinding.Of(operation), () => new TemplateApplication(tenant, program, applicationId, commandId),
                owner => owner.Acceptance,
                async (owner, token) =>
                {
                    if (owner.Header is { } existing && existing.ProgramId != program)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                            "The template application was not found."));
                    if (expectedRevision is { } expected && expected != owner.Revision)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The template application revision is stale."));
                    var snapshot = await applications.ReadAsync(owner, tenant, program, token).ConfigureAwait(false);
                    before = snapshot.View;
                    var rules = new TemplateApplicationRules(tenant, program, applicationId, before);
                    if (await prepare(rules, actor.MemberId, actor.Display, operation.AcceptedAt, token).ConfigureAwait(false) is { } failure)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    if (rules.PendingChanges.Count != 1)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                            "A new logical application operation must establish one new business fact."));
                    mutation = rules.PendingChanges[0];
                    after = rules.Current!;
                    header = rules.Header();
                    change = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        TemplateApplicationReader.Schema, mutation, token).ConfigureAwait(false);
                    var response = await UnitResponses.FreezeAsync(content, tenant.ToString(), token).ConfigureAwait(false);
                    outcome = await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(),
                        "mutation-result-content.v1", new MutationResultContent(response.ResultSchema, response.Body), token)
                        .ConfigureAwait(false);
                    revision = owner.Revision;
                    physical = owner.CommittedStreamPosition;
                    return Result<FirstResponseDescriptor>.Success(response);
                }, (owner, first) => owner.Append(Fact(kind, header!, change!, operation, first, outcome!), revision,
                    physical, mutation!, before, after!)), context, ct).ConfigureAwait(false);
            if (accepted.IsSuccess && accepted.Value is AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed)
            {
                var descriptor = accepted.Value switch
                {
                    AcceptedAppendResult.Committed committed => committed.Response,
                    AcceptedAppendResult.Replayed replayed => replayed.Response,
                    _ => throw new ImmutableContentIntegrityException("The application has another original outcome."),
                };
                if (descriptor.HttpStatus != 200 || descriptor.ResultSchema != UnitResult.Schema ||
                    descriptor.Body is not StandardResponseBody body || body.Content.Schema != UnitResult.Schema)
                    throw new ImmutableContentIntegrityException("The application has another original result schema.");
                _ = await content.ReadJsonAsync<UnitResult>(tenant.ToString(), body.Content, ct).ConfigureAwait(false);
            }
            return UnitResponses.ToResult(accepted);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The template application's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }

    static string IntentDigest<T>(T request) where T : IRequestBase => request switch
    {
        ApplyMethodologyTemplate value => AuthoredIntentDigest.Compute(value with { ApplicationKey = value.ApplicationKey.Trim() }),
        ProposeTemplateSuccessorAdoption value => AuthoredIntentDigest.Compute(value with
        {
            ApplicationKey = value.ApplicationKey.Trim(),
            Disposition = (value.Disposition?.Trim() ?? string.Empty).ToLowerInvariant(),
        }),
        RecordTemplateSuccessorDisposition value => AuthoredIntentDigest.Compute(value with
        {
            ApplicationKey = value.ApplicationKey.Trim(),
            Disposition = (value.Disposition?.Trim() ?? string.Empty).ToLowerInvariant(),
            Rationale = value.Rationale?.Trim() ?? string.Empty,
        }),
        _ => throw new InvalidOperationException("The application has another authored command."),
    };

    static DomainEvent Fact(string kind, TemplateApplicationHeader header, ImmutableContentRef content,
        OperationProvenance operation, ImmutableContentRef first, ImmutableContentRef result) => kind switch
        {
            nameof(ApplyMethodologyTemplate) => new MethodologyTemplateApplied(header.TenantId, header.TemplateApplicationId,
                header.Revision, header.ActorMemberId, header, content, header.UpdatedAt, operation, first, result),
            nameof(ProposeTemplateSuccessorAdoption) => new TemplateSuccessorAdoptionProposed(header.TenantId,
                header.TemplateApplicationId, header.Revision, header.ActorMemberId, header, content, header.UpdatedAt,
                operation, first, result),
            nameof(RecordTemplateSuccessorDisposition) => new TemplateSuccessorDispositionRecorded(header.TenantId,
                header.TemplateApplicationId, header.Revision, header.ActorMemberId, header, content, header.UpdatedAt,
                operation, first, result),
            _ => throw new InvalidOperationException("The application has another selected native command."),
        };
}
