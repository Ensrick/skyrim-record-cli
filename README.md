# skyrim-record-cli

Headless record inspection utilities built on Mutagen.Bethesda. The first command
exports weapon balance fields as JSON Lines so modded records can be compared against
the installed game's actual masters instead of relying on wiki tables or memory.
The `audit-links` command resolves every form link in a plugin against its master and
the plugin itself, failing if a removed or malformed record leaves a dangling link.
The `plugin-info` command reports declared masters and a record-type inventory as
JSON without requiring xEdit or a graphical application.
The `records` command emits a stable JSON Lines inventory of every major record for
headless comparisons between plugin revisions and patches.

Field inspection is available without opening xEdit:

```text
skyrim-record-cli record-fields <plugin> <FormKey-or-EditorID>
skyrim-record-cli record-fields-by-type <plugin> <record-type>
skyrim-record-cli record-selected-fields-by-type <plugin> <record-type> <comma-separated-fields>
```

`record-fields` emits one indented JSON record. The by-type forms load the plugin
once and emit JSON Lines, making complete semantic conflict sweeps practical in
the background. The selected-field form avoids serializing child groups and
other unrelated data when only record-header fields are under review.

## Exact distribution-graph inspection

```text
skyrim-record-cli leveled-items <plugin>
skyrim-record-cli record-links <plugin>
skyrim-record-cli self-test-leveled-items
```

`leveled-items` emits every LVLI with exact entry target FormKeys, levels,
counts, flags and chance-none (a fraction from 0 to 1), plus its optional
`chanceNoneGlobalFormKey`. A non-null global must be resolved before estimating
chance-none probability; the static fraction alone is insufficient.
Entry order/duplicates are preserved, with no
64-entry reflection truncation; missing data is explicit JSON null. A consumer
must handle missing data as an audit failure, not an empty valid entry.
`record-links` emits all non-null outgoing links of every major record for
inverse-reachability checks. Links are not proof of inventory use: field/context
inspection is still required to distinguish factions, outfits, scripts, etc.

Both commands are read-only, per-plugin JSON Lines. They do not infer the active
MO2 winner or runtime SkyPatcher changes. Consumers must assemble the current
ordered winners, apply reviewed runtime deltas, and inspect templates/consumers.
The self-test uses original synthetic records only.
