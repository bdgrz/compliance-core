using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>One unpublished candidate plus the local proofs its final atomic owner guard must bind.</summary>
sealed record PreparedPackageDelta(ImmutableContentRef Content, NativeFactReference? Build = null,
    NativeFactReference? Disclosure = null, IReadOnlyList<NativeFactReference>? Validations = null);
