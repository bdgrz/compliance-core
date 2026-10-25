using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed record PersonPrepared(ulong Version, ulong Position, string PrefixSha256, string MetadataSha256,
    PersonHeader? Before, ImmutableContentRef? BeforeContent, PersonView? BeforeView, PersonView View, ImmutableContentRef Content,
    PersonChangeContent Mutation, ImmutableContentRef Change, bool Changed,
    FirstResponseDescriptor Response, ImmutableContentRef Result);
