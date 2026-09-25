using iptv.Domain.Repositories.Contracts;
using MongoDB.Driver;

namespace iptv.Services._Startup;

/// <summary>
/// Runs the legacy channel/stream dedupe once, recording <see cref="Name"/> in the Migrations
/// collection. It is also re-run when a unique index cannot be created because of duplicate keys.
/// </summary>
internal static class DedupeMigration
{
    public const string Name = "dedupe-v1";

    /// <summary>Runs <paramref name="dedupe"/> unless the marker exists. Returns true when it ran.</summary>
    public static async Task<bool> RunOnceAsync(
        IMigrationRepository migrations, Func<Task> dedupe, CancellationToken ct)
    {
        if (await migrations.IsAppliedAsync(Name, ct))
            return false;

        await dedupe();
        await migrations.MarkAppliedAsync(Name, "startup", ct);
        return true;
    }

    /// <summary>True for Mongo's E11000 duplicate-key error (as raised by a unique index build).</summary>
    public static bool IsDuplicateKeyError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            switch (e)
            {
                case MongoCommandException cmd when cmd.Code == 11000 || cmd.Code == 11001:
                case MongoWriteException w when w.WriteError?.Category == ServerErrorCategory.DuplicateKey:
                    return true;
            }

            if (e.Message?.Contains("E11000", StringComparison.Ordinal) == true)
                return true;
        }

        return false;
    }
}
