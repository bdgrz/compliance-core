using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>Reads and writes the immutable content referenced by staff record facts.</summary>
static class FirmStaffContent
{
    /// <summary>Realm of every platform staff stream and its content.</summary>
    public const string Realm = "bdgrz";

    /// <summary>Schema of the complete <see cref="FirmStaffMemberView" /> carried by a change fact.</summary>
    public const string StaffSchema = "FirmStaffChangeRecorded.StaffContent.v1";

    public static Task<ImmutableContentRef> WriteStaffAsync(IImmutableContentStore content, Uuid contentId,
        FirmStaffMemberView staff, CancellationToken ct) =>
        content.WriteJsonAsync(Realm, contentId, StaffSchema, staff, ct);

    public static Task<ImmutableContentRef> WriteResultAsync(IImmutableContentStore content, Uuid contentId,
        ImmutableContentRef staffContent, CancellationToken ct) =>
        content.WriteJsonAsync(Realm, contentId, "mutation-result-content.v1",
            new MutationResultContent(StaffSchema, new StandardResponseBody(staffContent)), ct);

    public static Task<FirmStaffMemberView> ReadStaffAsync(IImmutableContentStore content,
        ImmutableContentRef reference, CancellationToken ct) =>
        content.ReadJsonAsync<FirmStaffMemberView>(Realm, reference, ct);

    public static Task<FirmStaffRegistrationIntent> ReadIntentAsync(IImmutableContentStore content,
        ImmutableContentRef reference, CancellationToken ct) =>
        content.ReadJsonAsync<FirmStaffRegistrationIntent>(Realm, reference, ct);

    /// <summary>The staff view a registration intent produces when its completion is recorded.</summary>
    public static FirmStaffMemberView RegisteredView(FirmStaffRegistrationIntent intent) => new(intent.StaffMemberId,
        intent.CanonicalUserId, intent.Practice, intent.SourceReference, true, 1, intent.Header.OriginalActor,
        intent.Header.AcceptedAt);

    public static void RequireIntent(FirmStaffRecord owner, FirmStaffRegistrationIntent intent)
    {
        if (owner.Accepted is not { } accepted || intent.StaffMemberId != owner.StaffMemberId ||
            intent.CanonicalUserId == Uuid.Empty || intent.Practice is not ("advisory" or "attest") ||
            !Bounded(intent.SourceReference, 2000) || intent.InitialStatus != FirmStaffRegistrationIntent.ActiveStatus ||
            intent.Header.RootOperationId != owner.StaffMemberId || intent.Header.FirstOwner != owner.Stream ||
            intent.Header.AffectedOwner != owner.Stream || intent.Header.ExpectedRevisionOwner != owner.Stream ||
            intent.Header.OriginalActor != accepted.Operation.Actor || intent.Header.Episode != accepted.Operation.Episode ||
            intent.Header.OriginalExpectedRevision != accepted.Operation.OriginalExpectedRevision ||
            intent.Header.AuthoredIntentSha256 != accepted.Operation.AuthoredIntentSha256 ||
            intent.Header.AcceptedAt != accepted.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The immutable staff identity differs from its exact native registration admission.");
    }

    /// <summary>Bounded text: non-blank, trimmed, no control characters.</summary>
    public static bool Bounded(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
}
