using Cntryl.Portia;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Pins a real V4 publication and plans only new drafts or changed-entry revision proposals.</summary>
public sealed class ApplyMethodologyTemplateV4Handler(IAggregateExecutor executor, IAggregateReader reader,
    MethodologyTemplateLineReader templates, ProgramApplicationAdmission programs,
    ApplyMethodologyTemplateV4Authorizer authorization) : IRequestHandler<ApplyMethodologyTemplateV4>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyMethodologyTemplateV4> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.TemplateId == Uuid.Empty || request.TemplateVersion <= 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "Application requires exact tenant, program, template, version and command identities."));
        var access = await authorization.AuthorizeAsync(context, ct).ConfigureAwait(false);
        if (!access.IsSuccess)
            return access;
        var active = await programs.CheckAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        if (!active.IsSuccess)
            return active;
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var appliedBy))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "A personal application actor is required."));
        var current = await reader.HydrateAsync(new TemplateApplication(request.TenantId, request.ProgramId, request.TemplateId, true), ct)
            .ConfigureAwait(false);
        var prior = current.AppliedVersionFor(request.CommandId);
        if (prior is not null)
            return await executor.ExecuteAsync(new TemplateApplication(request.TenantId, request.ProgramId, request.TemplateId, true),
                owner => AggregateOutcome.CommitOnSuccess(owner.ApplyVersion(request.CommandId, request.TemplateVersion,
                    appliedBy, prior.PublicationSource, prior.PublicationPosition, prior.EntriesContent,
                    prior.EntriesToCreate, prior.EntriesToPropose)), context, ct).ConfigureAwait(false);
        var publication = await templates.PublicationAsync(request.TemplateId, ct).ConfigureAwait(false);
        if (publication is null || publication.Line.Status == "retired")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Only a current nonretired methodology line may be applied."));
        var version = await templates.VersionAsync(request.TemplateId, request.TemplateVersion, ct).ConfigureAwait(false);
        if (version is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The published methodology version was not found."));
        var previous = current.LastAppliedVersion == 0 ? null
            : await templates.VersionAsync(request.TemplateId, current.LastAppliedVersion, ct).ConfigureAwait(false);
        if (current.LastAppliedVersion != 0 && previous is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The previous applied publication is unavailable.", isTransient: true));
        var (create, propose) = TemplateApplicationEntries.Plan(previous?.Entries, version.Entries);
        return await executor.ExecuteAsync(new TemplateApplication(request.TenantId, request.ProgramId, request.TemplateId, true),
            owner => AggregateOutcome.CommitOnSuccess(owner.LastAppliedVersion != current.LastAppliedVersion
                ? Result.Failure(new RequestError(RequestErrorKind.Conflict, "The prior applied version changed during planning.", isTransient: true))
                : owner.ApplyVersion(request.CommandId, request.TemplateVersion, appliedBy, version.PublicationSource,
                    version.PublicationPosition, version.EntriesContent, create, propose)), context, ct).ConfigureAwait(false);
    }
}
