using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class ListTenantInvitationsHandler(ITenantInvitationDirectoryReader directory,
    ITenantMembershipDirectoryReader memberships, ITenantInvitationDeliveryStore deliveries,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<ListTenantInvitations, Page<TenantInvitationView>>
{
    public async ValueTask<Result<Page<TenantInvitationView>>> HandleAsync(
        IRequestContext<ListTenantInvitations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Failure(RequestErrorKind.Validation,
                "The tenant invitation list limit must be between 1 and 200.");
        if (request.EmailAddress is not null && request.Cursor is not null)
            return Failure(RequestErrorKind.Validation,
                "The tenant invitation cursor cannot be used with an email address filter.");
        var now = clock.GetUtcNow();
        if (request.EmailAddress is not null)
        {
            if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
                return Failure(RequestErrorKind.Validation, "Enter a valid email address.");
            var invitation = await directory.GetAsync(request.TenantId, normalized, ct)
                .ConfigureAwait(false);
            if (invitation is null)
                return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>([], null));
            if (invitation.TenantId != request.TenantId)
                return Failure(RequestErrorKind.NotFound, "The tenant invitations were not found.");
            var view = await ToViewAsync(invitation, now, ct).ConfigureAwait(false);
            return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>([view], null));
        }

        Page<TenantInvitationDirectoryEntry> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Failure(RequestErrorKind.Validation, "The tenant invitation cursor is invalid.");
        }
        if (page.Items.Any(invitation => invitation.TenantId != request.TenantId))
            return Failure(RequestErrorKind.NotFound, "The tenant invitations were not found.");
        var views = new List<TenantInvitationView>(page.Items.Count);
        foreach (var entry in page.Items)
            views.Add(await ToViewAsync(entry, now, ct).ConfigureAwait(false));
        return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>(
            views, page.NextCursor));
    }

    async ValueTask<TenantInvitationView> ToViewAsync(TenantInvitationDirectoryEntry entry,
        DateTimeOffset now, CancellationToken ct)
    {
        var status = now >= entry.ExpiresAt ? "expired" : "pending";
        if (entry.AcceptedUserId is { } userId)
        {
            status = "accepted_pending_activation";
            // Invitation activation is complete once registration projects. Suspension
            // revokes access but does not undo that accepted registration.
            var member = await memberships.GetAsync(entry.TenantId.ToString(), userId, ct)
                .ConfigureAwait(false);
            if (member is not null && member.TenantId == entry.TenantId &&
                member.UserId == userId)
                status = "active";
        }
        if (entry.GuestExpiresOn is { } guestExpiresOn &&
            DateOnly.FromDateTime(now.UtcDateTime) > guestExpiresOn)
            status = "expired";
        var delivery = await deliveries.ReadAsync(entry.TenantId, entry.InvitationId, ct).ConfigureAwait(false);
        IReadOnlyList<Uuid>? programIds = null;
        if (entry.GuestProgramIdsManifest is { } manifest)
            programIds = (await content.ReadJsonAsync<IReadOnlyList<Uuid>>(entry.TenantId.ToString(), manifest, ct)
                .ConfigureAwait(false)).ToArray();
        return new TenantInvitationView(entry.TenantId, entry.EmailAddress,
            entry.Affiliation, entry.Administrator, entry.BuiltInRole, status,
            entry.ExpiresAt, entry.InvitedBy, entry.AcceptedUserId,
            programIds, entry.GuestExpiresOn)
        {
            InvitationId = entry.InvitationId,
            DeliveryStatus = delivery?.Status ?? TenantInvitationDeliveryRecord.Pending,
        };
    }

    static Result<Page<TenantInvitationView>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<TenantInvitationView>>.Failure(new RequestError(kind, message));
}
