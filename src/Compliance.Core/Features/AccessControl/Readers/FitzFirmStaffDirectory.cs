using Cntryl.Fitz;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Readers;

sealed class FitzFirmStaffDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/firm-staff-directory-v1/projection", ProjectorName),
        IFirmStaffDirectoryProjection, IFirmStaffDirectoryReader
{
    public const string ProjectorName = "FirmStaffDirectoryV1";
    const int PageSize = 200;

    public async ValueTask ApplyAsync(FirmStaffMemberView staff, CancellationToken ct = default)
    {
        var current = await FirmStaffDirectorySchema.Staff.GetAsync(Transaction, staff.StaffMemberId, ct)
            .ConfigureAwait(false);
        if (current is null)
        {
            if (staff.Revision != 1)
                throw new InvalidOperationException("A staff record cannot project before its registration.");
            await FirmStaffDirectorySchema.Staff.InsertAsync(Transaction, staff, ct).ConfigureAwait(false);
            return;
        }

        if (staff.Revision <= current.Revision)
            return;
        if (staff.Revision != current.Revision + 1 || staff.UserId != current.UserId ||
            staff.Practice != current.Practice || staff.SourceReference != current.SourceReference)
            throw new InvalidOperationException("A staff change must follow its predecessor and keep its identity.");
        await FirmStaffDirectorySchema.Staff.ReplaceAsync(Transaction, current, staff, ct).ConfigureAwait(false);
    }

    public async ValueTask<FirmStaffDirectoryView> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var staff = new List<FirmStaffMemberView>();
        string? cursor = null;
        do
        {
            await using var tx = await BeginReadAsync(FirmStaffContent.Realm, ct).ConfigureAwait(false);
            var page = await FirmStaffDirectorySchema.Staff.QueryAsync(tx, FirmStaffDirectorySchema.ById.Query()
                .Take(PageSize).After(cursor), ct).ConfigureAwait(false);
            staff.AddRange(page.Items.Where(item => !activeOnly || item.IsActive));
            cursor = page.NextCursor;
        } while (cursor is not null);
        return new FirmStaffDirectoryView(Array.AsReadOnly(staff
            .OrderBy(item => item.StaffMemberId.ToString(), StringComparer.Ordinal).ToArray()));
    }
}
