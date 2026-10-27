using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots.Queries;

[Discriminator("bdgrz.snapshot.program.list", 1)]
public sealed record ListProgramSnapshots(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<SnapshotView>>, IProgramReadRequest, ICallable;
