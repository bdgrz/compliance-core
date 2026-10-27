using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzFirmProfessionalDutyDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/firm-professional-duty-directory-v1/projection", ProjectorName),
        IFirmProfessionalDutyDirectoryProjection, IFirmProfessionalDutyDirectoryReader
{
    public const string ProjectorName = "FirmProfessionalDutyDirectoryV1";
    const int PageSize = 200;

    public async ValueTask ApplyAsync(FirmProfessionalDutyDesignationView designation, CancellationToken ct = default)
    {
        var owner = FirmProfessionalDutyDesignation.IdentityOf(designation.UserId, designation.Duty,
            designation.TenantId);
        var current = await FirmProfessionalDutyDirectorySchema.Designations.GetAsync(Transaction, owner, ct)
            .ConfigureAwait(false);
        if (current is null)
        {
            await FirmProfessionalDutyDirectorySchema.Designations.InsertAsync(Transaction, designation, ct)
                .ConfigureAwait(false);
            return;
        }

        if (designation.Revision <= current.Revision)
            return;
        await FirmProfessionalDutyDirectorySchema.Designations.ReplaceAsync(Transaction, current, designation, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<FirmProfessionalDutyCatalogView> ListAsync(CancellationToken ct = default)
    {
        var designations = new List<FirmProfessionalDutyDesignationView>();
        string? cursor = null;
        do
        {
            await using var tx = await BeginReadAsync(ProfessionalDutyContent.Realm, ct).ConfigureAwait(false);
            var page = await FirmProfessionalDutyDirectorySchema.Designations.QueryAsync(tx,
                FirmProfessionalDutyDirectorySchema.ByOwner.Query().Take(PageSize).After(cursor), ct)
                .ConfigureAwait(false);
            designations.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return new FirmProfessionalDutyCatalogView(Array.AsReadOnly(designations
            .OrderBy(item => item.DesignationId.ToString(), StringComparer.Ordinal).ToArray()));
    }
}
