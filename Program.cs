using System.Text.Json;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins.Records;

void EmitWeapons(ISkyrimModGetter mod, string source)
{
    foreach (var weapon in mod.Weapons.OrderBy(w => w.EditorID))
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            source,
            formKey = weapon.FormKey.ToString(),
            editorId = weapon.EditorID,
            name = weapon.Name?.String,
            model = weapon.Model?.File?.ToString(),
            keywords = weapon.Keywords?.Select(k => k.FormKey.ToString()).ToArray(),
            value = weapon.BasicStats?.Value,
            weight = weapon.BasicStats?.Weight,
            damage = weapon.BasicStats?.Damage,
            speed = weapon.Data?.Speed,
            reach = weapon.Data?.Reach,
            stagger = weapon.Data?.Stagger,
            animationType = weapon.Data?.AnimationType.ToString(),
            skill = weapon.Data?.Skill.ToString(),
            equipmentType = weapon.EquipmentType.FormKey.ToString(),
            criticalDamage = weapon.Critical?.Damage,
        }));
    }
}

if (args.Length == 3 && args[0] == "audit-links")
{
    using var master = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[2], SkyrimRelease.SkyrimSE);
    var cache = new ISkyrimModGetter[] { master, plugin }.ToImmutableLinkCache();
    var unresolved = new List<object>();
    var linksChecked = 0;
    foreach (var record in plugin.EnumerateMajorRecords())
    {
        foreach (var link in record.EnumerateFormLinks())
        {
            if (link.FormKey.IsNull) continue;
            ++linksChecked;
            if (!cache.TryResolve(link.FormKey, link.Type, out _))
            {
                unresolved.Add(new
                {
                    record = record.FormKey.ToString(),
                    editorId = record.EditorID,
                    target = link.FormKey.ToString(),
                    targetType = link.Type.FullName,
                });
            }
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        records = plugin.EnumerateMajorRecords().Count(),
        linksChecked,
        unresolved,
    }));
    return unresolved.Count == 0 ? 0 : 2;
}

if (args.Length == 2 && args[0] == "scan-weapons")
{
    var paths = Directory.EnumerateFiles(args[1])
        .Where(path => new[] { ".esm", ".esp", ".esl" }.Contains(
            Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    foreach (var path in paths)
    {
        try
        {
            using var candidate = SkyrimMod.CreateFromBinaryOverlay(path, SkyrimRelease.SkyrimSE);
            EmitWeapons(candidate, Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Skipping {path}: {ex.Message}");
        }
    }
    return 0;
}

if (args.Length != 2 || args[0] != "weapons")
{
    Console.Error.WriteLine("Usage:\n  skyrim-record-cli weapons <plugin-path>\n  skyrim-record-cli scan-weapons <data-directory>\n  skyrim-record-cli audit-links <master-path> <plugin-path>");
    return 1;
}

using var mod = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
EmitWeapons(mod, Path.GetFileName(args[1]));

return 0;
