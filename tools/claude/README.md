# Arbeitsumgebung für Claude

Claude arbeitet in einer abgeschotteten Linux-Umgebung, in der die Downloadserver von Microsoft und nuget.org
gesperrt sind, GitHub aber erreichbar ist. Damit Claude trotzdem alles selbst bauen und testen kann:

1. Der Workflow **„Werkzeuge für Claude“** (`.github/workflows/werkzeuge.yml`) legt das offizielle .NET-SDK und alle
   NuGet-Pakete der Solution im Release `claude-werkzeuge` ab. Er läuft automatisch, sobald sich Projektdateien auf
   `main` ändern, und kann von Hand gestartet werden.
2. `tools/claude/fetch-reference.sh` lädt die Originaldateien und die Referenzschrift aus dem privaten Repo
   `kaaasi/bomb-referenz` nach `~/referenz/original/` bzw. `~/referenz/schriften/` und prüft die Prüfsummen.
3. `tools/claude/setup-workspace.sh ~/referenz/original/gamedata` lädt SDK und Pakete, richtet eine
   Offline-Paketquelle ein und verlinkt die Originaldateien nach `gamedata/` (lädt sie nicht selbst).
4. Danach: `source ~/claude-tools/env.sh`, dann `tools/pruefen.sh --schnell`.

In dieses Code-Repository und in dessen Werkzeug-Release gehören keine Originaldateien. Das Release kann jederzeit
gelöscht werden; der nächste Workflow-Lauf erstellt es neu.
