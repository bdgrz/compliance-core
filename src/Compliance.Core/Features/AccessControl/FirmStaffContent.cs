using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

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

    /// <summary>Bounded text: non-blank, trimmed, no control characters.</summary>
    public static bool Bounded(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl) && value == value.Trim();
}
