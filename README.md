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

The `npc-loadouts` command expands each NPC's direct inventory and default outfit,
including item counts and resolved record types/editor IDs. Pass master or patch
plugins after the subject plugin when their linked records also need resolution:

```text
skyrim-record-cli npc-loadouts <plugin> [record-source ...]
```

This makes it possible to distinguish an intentionally unarmed actor using race
attacks from an NPC whose weapon or outfit link was lost, without opening xEdit.

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
