# skyrim-record-cli

Headless record inspection utilities built on Mutagen.Bethesda. The first command
exports weapon balance fields as JSON Lines so modded records can be compared against
the installed game's actual masters instead of relying on wiki tables or memory.
The `audit-links` command resolves every form link in a plugin against its master and
the plugin itself, failing if a removed or malformed record leaves a dangling link.
