using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Rules;

/// <summary>Transient business evaluation over a verified complete source cut; never a second authority.</summary>
public static class PlatformOperatorRosterRules
{
    public static Result<PlatformOperatorAuthorityContent> Apply(PlatformOperatorAuthorityContent before,
        PlatformOperatorMutationContent mutation)
    {
        if (mutation.CommandId == Uuid.Empty || mutation.OriginalActor is null || mutation.Actor is null ||
            string.IsNullOrWhiteSpace(mutation.OriginalActor.Subject) || string.IsNullOrWhiteSpace(mutation.OriginalActor.Issuer) ||
            mutation.AcceptedAt == default)
            return Failure(RequestErrorKind.Validation, "A selected command identity and original actor are required.");
        var operators = before.Operators.ToDictionary(static item => item.UserId, static item => item.AssignmentId);
        if (mutation.CommandKind == nameof(SeedPlatformOperatorRoster))
        {
            if (mutation.CommandId != PlatformOperatorRoster.CreationId || mutation.ExpectedRevision is not null ||
                mutation.ActorUserId != Uuid.Empty || mutation.SubjectUserId != Uuid.Empty || mutation.AssignmentId != Uuid.Empty ||
                mutation.Reason.Length != 0 || mutation.Actor.Kind != "system_process" ||
                mutation.Actor.Id != "platform-operator-bootstrap" || mutation.BootstrapUserIds.Length == 0 ||
                mutation.BootstrapUserIds.Contains(Uuid.Empty) ||
                !mutation.BootstrapUserIds.SequenceEqual(CanonicalUsers(mutation.BootstrapUserIds)))
                return Failure(RequestErrorKind.Validation, "The fixed seed identity and canonical bootstrap users are required.");
            if (before.Revision != 0 || operators.Count != 0)
                return Failure(RequestErrorKind.Conflict, "The initial roster already exists.");
            foreach (var user in mutation.BootstrapUserIds)
                operators.Add(user, Uuid.CreateVersion5(PlatformOperatorRoster.CreationId, $"platform-operator:initial:{user}"));
        }
        else
        {
            if (!operators.ContainsKey(mutation.ActorUserId))
                return Failure(RequestErrorKind.Forbidden, "The actor is not a platform operator.");
            if (mutation.SubjectUserId == Uuid.Empty || mutation.Actor.Kind != "platform_operator" ||
                mutation.Actor.Id != mutation.ActorUserId.ToString() ||
                mutation.OriginalActor.Subject != mutation.ActorUserId.ToString() ||
                !ValidReason(mutation.Reason) || mutation.Reason != mutation.Reason.Trim() || mutation.BootstrapUserIds.Length != 0)
                return Failure(RequestErrorKind.Validation, "An attributed platform user and bounded canonical reason are required.");
            if (mutation.ExpectedRevision != before.Revision)
                return Failure(RequestErrorKind.Conflict, "The operator roster revision changed.");
            if (mutation.CommandKind == nameof(GrantPlatformOperator))
            {
                if (mutation.AssignmentId != Uuid.Empty)
                    return Failure(RequestErrorKind.Validation, "A grant cannot name another assignment episode.");
                if (!operators.TryAdd(mutation.SubjectUserId, mutation.CommandId))
                    return Failure(RequestErrorKind.Conflict, "The user is already assigned.");
            }
            else if (mutation.CommandKind == nameof(RevokePlatformOperator))
            {
                if (mutation.AssignmentId == Uuid.Empty)
                    return Failure(RequestErrorKind.Validation, "An operator assignment identity is required.");
                if (!operators.TryGetValue(mutation.SubjectUserId, out var assignment) || assignment != mutation.AssignmentId)
                    return Failure(RequestErrorKind.Conflict, "The operator assignment episode changed.");
                if (operators.Count == 1)
                    return Failure(RequestErrorKind.Conflict, "The last platform operator cannot be revoked.");
                operators.Remove(mutation.SubjectUserId);
            }
            else
                return Failure(RequestErrorKind.Validation, "Unknown roster operation.");
        }
        return Result<PlatformOperatorAuthorityContent>.Success(new(PlatformOperatorRoster.CreationId, before.Revision + 1,
            operators.OrderBy(static entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(static entry => new PlatformOperatorAssignmentView(entry.Key, entry.Value)).ToArray()));
    }

    public static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;
    public static Uuid[] CanonicalUsers(IEnumerable<Uuid> users) => users.Distinct()
        .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
    static Result<PlatformOperatorAuthorityContent> Failure(RequestErrorKind kind, string message) =>
        Result<PlatformOperatorAuthorityContent>.Failure(new RequestError(kind, message));
}
