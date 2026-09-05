using System.Text.Json;
using System.Collections;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Plugins;

object? SummarizeValue(object? value, int depth = 0)
{
    if (value is null) return null;
    var type = value.GetType();
    if (value is string || type.IsPrimitive || type.IsEnum || value is decimal)
        return type.IsEnum ? value.ToString() : value;
    if (depth >= 2) return value.ToString();
    if (value is IEnumerable enumerable)
    {
        var items = new List<object?>();
        foreach (var item in enumerable)
        {
            items.Add(SummarizeValue(item, depth + 1));
            if (items.Count == 64)
            {
                items.Add("<truncated>");
                break;
            }
        }
        return items;
    }

    var properties = type.GetProperties()
        .Where(property => property.CanRead
            && property.GetIndexParameters().Length == 0
            && property.Name is not ("TopCell" or "SubCells" or "Registration" or "MetaInterfaceMap"))
        .OrderBy(property => property.Name, StringComparer.Ordinal);
    var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
    foreach (var property in properties)
    {
        try
        {
            result[property.Name] = SummarizeValue(property.GetValue(value), depth + 1);
        }
        catch (Exception exception)
        {
            result[property.Name] = $"<unreadable: {exception.GetType().Name}>";
        }
    }
    return result.Count == 0 ? value.ToString() : result;
}

string RecordTypeName(IMajorRecordGetter record) =>
    record.GetType().Name.Replace("BinaryOverlay", string.Empty);

object RecordFields(IMajorRecordGetter record, string plugin) => new
{
    plugin,
    formKey = record.FormKey.ToString(),
    type = RecordTypeName(record),
    editorId = record.EditorID,
    fields = SummarizeValue(record),
};

object SelectedRecordFields(
    IMajorRecordGetter record,
    string plugin,
    IReadOnlyCollection<string> selectedFields)
{
    var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
    var properties = record.GetType().GetProperties()
        .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
        .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
    foreach (var field in selectedFields.OrderBy(field => field, StringComparer.Ordinal))
    {
        if (!properties.TryGetValue(field, out var property))
        {
            fields[field] = "<field-not-found>";
            continue;
        }
        try
        {
            // Match the nesting level used when summarizing a complete record;
            // otherwise link wrapper properties expand into reflection metadata.
            fields[field] = SummarizeValue(property.GetValue(record), 1);
        }
        catch (Exception exception)
        {
            fields[field] = $"<unreadable: {exception.GetType().Name}>";
        }
    }
    return new
    {
        plugin,
        formKey = record.FormKey.ToString(),
        type = RecordTypeName(record),
        editorId = record.EditorID,
        fields,
    };
}

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

string LeveledItemJson(ILeveledItemGetter item, string plugin) =>
    JsonSerializer.Serialize(new
    {
        plugin,
        type = "LeveledItem",
        formKey = item.FormKey.ToString(),
        editorId = item.EditorID,
        chanceNone = item.ChanceNone.Value,
        chanceNoneGlobalFormKey = item.Global.FormKey.IsNull ? null : item.Global.FormKey.ToString(),
        flags = item.Flags.ToString(),
        entries = item.Entries?.Select(entry => new
        {
            level = entry.Data?.Level,
            count = entry.Data?.Count,
            referenceFormKey = entry.Data is null || entry.Data.Reference.FormKey.IsNull
                ? null : entry.Data.Reference.FormKey.ToString(),
        }).ToArray(),
    });

if (args.Length == 1 && args[0] == "self-test-leveled-items")
{
    // Original synthetic fixture: no Bethesda or vendor payload required.
    var item = new LeveledItem(FormKey.Factory("000800:Synthetic.esp"), SkyrimRelease.SkyrimSE);
    item.EditorID = "SyntheticList";
    item.ChanceNone = new Noggog.Percent(0.15);
    item.Flags = LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
    item.Entries = new();
    var empty = JsonDocument.Parse(LeveledItemJson(item, "Synthetic.esp")).RootElement;
    if (empty.GetProperty("entries").GetArrayLength() != 0
        || Math.Abs(empty.GetProperty("chanceNone").GetDouble() - 0.15) > 0.000001)
        throw new InvalidOperationException("Empty list/chance-none export failed");
    var target = FormKey.Factory("000ABC:Target.esm");
    for (var index = 0; index < 70; index++)
    {
        var entry = new LeveledItemEntry { Data = new LeveledItemEntryData { Level = 7, Count = 2 } };
        entry.Data.Reference.SetTo(target);
        item.Entries.Add(entry);
    }
    var json = LeveledItemJson(item, "Synthetic.esp");
    using var parsed = JsonDocument.Parse(json);
    var root = parsed.RootElement;
    var entries = root.GetProperty("entries");
    if (entries.GetArrayLength() != 70
        || entries[0].GetProperty("referenceFormKey").GetString() != target.ToString()
        || entries[0].GetProperty("level").GetInt32() != 7
        || entries[0].GetProperty("count").GetInt32() != 2
        || root.GetProperty("formKey").GetString() != item.FormKey.ToString()
        || !root.GetProperty("flags").GetString()!.Contains("CalculateFromAllLevels"))
        throw new InvalidOperationException("Exact leveled-item export failed");
    if (json != LeveledItemJson(item, "Synthetic.esp"))
        throw new InvalidOperationException("Export is not deterministic");
    item.Entries.Add(new LeveledItemEntry());
    using var missing = JsonDocument.Parse(LeveledItemJson(item, "Synthetic.esp"));
    if (missing.RootElement.GetProperty("entries")[70].GetProperty("referenceFormKey").ValueKind
        != JsonValueKind.Null)
        throw new InvalidOperationException("Missing entry data was not represented as null");
    item.Entries.Add(new LeveledItemEntry { Data = new LeveledItemEntryData() });
    item.Global.SetTo(FormKey.Factory("000FED:Target.esm"));
    using var global = JsonDocument.Parse(LeveledItemJson(item, "Synthetic.esp"));
    if (global.RootElement.GetProperty("entries")[71].GetProperty("referenceFormKey").ValueKind
        != JsonValueKind.Null
        || global.RootElement.GetProperty("chanceNoneGlobalFormKey").GetString() != "000FED:Target.esm")
        throw new InvalidOperationException("Null reference / chance-none global export failed");
    Console.WriteLine("Synthetic leveled-items tests passed: empty, flags, exact links/levels/counts, 70-entry non-truncation, deterministic, missing-data null.");
    return 0;
}

if (args.Length == 2 && args[0] == "leveled-items")
{
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    foreach (var item in plugin.LeveledItems.OrderBy(item => item.FormKey.ToString(), StringComparer.Ordinal))
        Console.WriteLine(LeveledItemJson(item, Path.GetFileName(args[1])));
    return 0;
}

if (args.Length == 2 && args[0] == "record-links")
{
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    foreach (var record in plugin.EnumerateMajorRecords()
                 .OrderBy(record => record.FormKey.ToString(), StringComparer.Ordinal))
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            plugin = Path.GetFileName(args[1]),
            formKey = record.FormKey.ToString(),
            type = RecordTypeName(record),
            editorId = record.EditorID,
            links = record.EnumerateFormLinks().Where(link => !link.FormKey.IsNull)
                .Select(link => new { formKey = link.FormKey.ToString(), type = link.Type.Name }).ToArray(),
        }));
    return 0;
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

if (args.Length == 2 && args[0] == "plugin-info")
{
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        plugin = Path.GetFileName(args[1]),
        masters = plugin.ModHeader.MasterReferences
            .Select(reference => reference.Master.FileName.String)
            .ToArray(),
        records = plugin.EnumerateMajorRecords().Count(),
        recordTypes = plugin.EnumerateMajorRecords()
            .GroupBy(record => record.GetType().Name)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count()),
    }));
    return 0;
}

if (args.Length == 2 && args[0] == "records")
{
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    foreach (var record in plugin.EnumerateMajorRecords()
                 .OrderBy(record => record.FormKey.ID))
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            formKey = record.FormKey.ToString(),
            type = RecordTypeName(record),
            editorId = record.EditorID,
        }));
    }
    return 0;
}

if (args.Length == 3 && args[0] == "record-fields")
{
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    var query = args[2];
    var record = plugin.EnumerateMajorRecords().FirstOrDefault(candidate =>
        string.Equals(candidate.FormKey.ToString(), query, StringComparison.OrdinalIgnoreCase)
        || string.Equals(candidate.EditorID, query, StringComparison.OrdinalIgnoreCase));
    if (record is null)
    {
        Console.Error.WriteLine($"Record not found: {query}");
        return 2;
    }
    Console.WriteLine(JsonSerializer.Serialize(
        RecordFields(record, Path.GetFileName(args[1])),
        new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

if (args.Length == 3 && args[0] == "record-fields-by-type")
{
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    var query = args[2];
    var records = plugin.EnumerateMajorRecords()
        .Where(record => string.Equals(
            RecordTypeName(record), query, StringComparison.OrdinalIgnoreCase))
        .OrderBy(record => record.FormKey.ID)
        .ToArray();
    foreach (var record in records)
    {
        Console.WriteLine(JsonSerializer.Serialize(
            RecordFields(record, Path.GetFileName(args[1]))));
    }
    return 0;
}

if (args.Length == 4 && args[0] == "record-selected-fields-by-type")
{
    using var plugin = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
    var query = args[2];
    var selectedFields = args[3].Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(field => field.Trim())
        .Where(field => field.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var records = plugin.EnumerateMajorRecords()
        .Where(record => string.Equals(
            RecordTypeName(record), query, StringComparison.OrdinalIgnoreCase))
        .OrderBy(record => record.FormKey.ID)
        .ToArray();
    foreach (var record in records)
    {
        Console.WriteLine(JsonSerializer.Serialize(
            SelectedRecordFields(record, Path.GetFileName(args[1]), selectedFields)));
    }
    return 0;
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
    Console.Error.WriteLine("Distribution graph commands:\n  skyrim-record-cli leveled-items <plugin-path>\n  skyrim-record-cli record-links <plugin-path>\n  skyrim-record-cli self-test-leveled-items");
    Console.Error.WriteLine("Usage:\n  skyrim-record-cli weapons <plugin-path>\n  skyrim-record-cli scan-weapons <data-directory>\n  skyrim-record-cli audit-links <master-path> <plugin-path>\n  skyrim-record-cli plugin-info <plugin-path>\n  skyrim-record-cli records <plugin-path>\n  skyrim-record-cli record-fields <plugin-path> <FormKey-or-EditorID>\n  skyrim-record-cli record-fields-by-type <plugin-path> <record-type>\n  skyrim-record-cli record-selected-fields-by-type <plugin-path> <record-type> <comma-separated-fields>");
    return 1;
}

using var mod = SkyrimMod.CreateFromBinaryOverlay(args[1], SkyrimRelease.SkyrimSE);
EmitWeapons(mod, Path.GetFileName(args[1]));

return 0;
