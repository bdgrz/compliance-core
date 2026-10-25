using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Fails closed rather than substituting a mutable source read for the required held approval permits.</summary>
public static class SystemDescriptionApprovalQualification
{
    public static Result Check(SystemDescriptionReadSnapshot snapshot) => snapshot.CurrentVersion is { } version && (version.Content.Sources.Count == 0 || snapshot.Owner.SourceSnapshotContent is not null)
        ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "Approval of unqualified source history requires an exact verified immutable snapshot proof or the selected live-source authorization and settlement protocol, which is not implemented."));
}
