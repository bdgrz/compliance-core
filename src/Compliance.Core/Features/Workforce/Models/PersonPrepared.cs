using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Workforce.Models;

public sealed record PersonPrepared(ulong Version, ulong Position, string PrefixSha256, string MetadataSha256,
    PersonHeader? Before, ImmutableContentRef? BeforeContent, PersonView? BeforeView, PersonView View, ImmutableContentRef Content,
    PersonChangeContent Mutation, ImmutableContentRef Change, bool Changed,
    FirstResponseDescriptor Response, ImmutableContentRef Result);
