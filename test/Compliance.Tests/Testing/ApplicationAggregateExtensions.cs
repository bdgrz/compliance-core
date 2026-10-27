using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Calls the native owner methods the way a unit test needs them: each call is its own root operation on the
///     owner's stream with a placeholder frozen response, and the result keeps the shape the owner reported before
///     operations carried provenance.
/// </summary>
static class ApplicationAggregateExtensions
{
    public static readonly ImmutableContentRef Response = new(Uuid.CreateVersion4(), new string('a', 64), 2,
        "application/json", "first-response-descriptor.v1");

    public static Result<ApplicationRegistration> Declare(this DeclaredApplication app, string name, string purpose,
        string? ownerReference, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt,
        string? classification = null, Uuid? systemOwnerPersonId = null, Uuid? accessOwnerPersonId = null,
        bool isRestricted = false, string? applicationKind = null, string? criticality = null,
        bool governedDeclarationsEnabled = false)
    {
        var declared = app.Declare(ApplicationFacts.For(app), Response, name, purpose, ownerReference, actorMemberId,
            actorDisplay, changedAt, classification, systemOwnerPersonId, accessOwnerPersonId, isRestricted,
            applicationKind, criticality, governedDeclarationsEnabled);
        return declared.IsSuccess
            ? Result<ApplicationRegistration>.Success(new ApplicationRegistration(app.Id))
            : Result<ApplicationRegistration>.Failure(declared.Error!);
    }

    public static CommandFailure? Revise(this DeclaredApplication app, long expectedRevision, string name,
        string purpose, string? ownerReference, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt,
        string? classification = null, Uuid? systemOwnerPersonId = null, Uuid? accessOwnerPersonId = null,
        bool? isRestricted = null, string? applicationKind = null, string? criticality = null,
        bool governedRevisionsEnabled = false, ApplicationRevisionField classificationIntent = default,
        ApplicationRevisionField systemOwnerIntent = default, ApplicationRevisionField accessOwnerIntent = default) =>
        app.Revise(ApplicationFacts.For(app), Response, expectedRevision, name, purpose, ownerReference,
            actorMemberId, actorDisplay, changedAt, classification, systemOwnerPersonId, accessOwnerPersonId,
            isRestricted, applicationKind, criticality, governedRevisionsEnabled, classificationIntent,
            systemOwnerIntent, accessOwnerIntent);

    public static CommandFailure? Retire(this DeclaredApplication app, long expectedRevision,
        DateTimeOffset effectiveAt, string reason, Uuid? mergedIntoApplicationId, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt) =>
        app.Retire(ApplicationFacts.For(app), Response, expectedRevision, effectiveAt, reason,
            mergedIntoApplicationId, actorMemberId, actorDisplay, changedAt);

    public static Result<SystemInstanceRegistration> Declare(this DeclaredSystemInstance instance,
        Uuid applicationId, string name, string kind, string? accessBoundaryReference, string? sourceIdentifier,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        var declared = instance.Declare(ApplicationFacts.For(instance), Response, applicationId, name, kind,
            accessBoundaryReference, sourceIdentifier, actorMemberId, actorDisplay, changedAt);
        return declared.IsSuccess
            ? Result<SystemInstanceRegistration>.Success(new SystemInstanceRegistration(instance.Id))
            : Result<SystemInstanceRegistration>.Failure(declared.Error!);
    }

    public static CommandFailure? Revise(this DeclaredSystemInstance instance, long expectedRevision, string name,
        string? sourceIdentifier, string? accessBoundaryReference, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt) =>
        instance.Revise(ApplicationFacts.For(instance), Response, instance.ApplicationId, expectedRevision, name,
            sourceIdentifier, accessBoundaryReference, rationale, actorMemberId, actorDisplay, changedAt);

    public static CommandFailure? Retire(this DeclaredSystemInstance instance, long expectedRevision,
        DateTimeOffset effectiveAt, string reason, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt) =>
        instance.Retire(ApplicationFacts.For(instance), Response, instance.ApplicationId, expectedRevision,
            effectiveAt, reason, actorMemberId, actorDisplay, changedAt);
}
