using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The source proofs a participant result selected from its campaign at admission: the launch fact that froze the
///     exact material, the audience fact that included the person, and the facts the campaign derived from its
///     sources, the performing workforce person and whether the recorder acted on the person's behalf.
/// </summary>
public sealed record PolicyParticipantAdmissionProof(NativeFactReference Material, NativeFactReference Audience,
    ActorReference? Performer, bool RecordedOnBehalf);
