using Microsoft.EntityFrameworkCore;

namespace Cronus.Database;

/// <summary>
/// Copies every Cronus table from one database to another, keys included — the one-time move of a
/// legacy SQLite save into MySQL (the standard store since 2026-09-08), or any backup/restore
/// between providers. Tables go in dependency order (accounts → characters → items → …) so foreign
/// keys hold; ids are written as-is, which MySQL and SQLite both accept for auto-increment columns
/// and which keeps every cross-reference (character → account, item → character, parcel → character)
/// intact. The target is expected to be empty; existing rows with the same keys would fail.
/// </summary>
public static class DatabaseCopy
{
    /// <summary>Row counts copied per table.</summary>
    public sealed record Report(
        int Accounts,
        int Characters,
        int Items,
        int Storages,
        int Keymaps,
        int Guilds,
        int HiredMerchants,
        int Parcels)
    {
        public int Total => Accounts + Characters + Items + Storages + Keymaps + Guilds + HiredMerchants + Parcels;

        public override string ToString()
            => $"{Accounts} accounts, {Characters} characters, {Items} items, {Storages} storages, "
             + $"{Keymaps} keymaps, {Guilds} guilds, {HiredMerchants} hired merchants, {Parcels} parcels";
    }

    public static Report CopyAll(Func<CronusDbContext> source, Func<CronusDbContext> target)
    {
        using CronusDbContext from = source();
        using CronusDbContext to = target();
        to.ChangeTracker.AutoDetectChangesEnabled = false;

        // Characters are loaded without their EquippedItems navigation (AsNoTracking, no Include),
        // so the items travel as their own rows in the next step with CharacterId already set.
        int accounts = Copy(from.Accounts, to);
        int characters = Copy(from.Characters, to);
        int items = Copy(from.Items, to);
        int storages = Copy(from.Storages, to);
        int keymaps = Copy(from.Keymaps, to);
        int guilds = Copy(from.Guilds, to);
        int merchants = Copy(from.HiredMerchants, to);
        int parcels = Copy(from.Parcels, to);

        return new Report(accounts, characters, items, storages, keymaps, guilds, merchants, parcels);
    }

    private static int Copy<T>(DbSet<T> from, CronusDbContext to)
        where T : class
    {
        List<T> rows = from.AsNoTracking().ToList();
        if (rows.Count == 0)
        {
            return 0;
        }

        to.Set<T>().AddRange(rows);
        to.SaveChanges();
        to.ChangeTracker.Clear();
        return rows.Count;
    }
}
