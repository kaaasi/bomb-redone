# Übergabe – Vorlage

Als **Kommentar im PR** am Ende jeder Arbeitssitzung, fertig wie bei Abbruch (`docs/prozess/zusammenarbeit.md` §3); ohne PR als
`docs/handoffs/<JJJJ-MM-TT>-<thema>.md` auf eigenem Branch. **Höchstens 15 Zeilen plus Testtabelle**; Details im PR nur verlinken,
nicht ausgeführte Läufe der Tabelle weglassen.

```markdown
## Übergabe

- **Von → an:** <Agent> → <Agent | frei>
- **Branch / PR:** paket/<spur> / #<nr>
- **Base-SHA:** <sha von main, auf dem der Branch aufbaut>
- **Head-SHA:** <letzter gepushter Commit> – alles gepusht, keine lokalen Änderungen: ja
- **Arbeitspaket:** <Abschnitt in docs/arbeitspakete.md>, Befunde <IDs>

### Erledigt
- …

### Offen
- …

### Nächster konkreter Schritt
- …

### Tests zum Head-SHA (`tools/pruefen.sh`)
| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten | | | |
| Ohne Originaldaten | | | |
| Pflichtlauf ohne Daten (muss scheitern) | | | |

### Risiken, Annahmen, neue [APPROX]
- …

**Bearbeiter:** frei
```

Wer übernimmt, antwortet mit einem Übernahmekommentar:

```markdown
## Übernahme
- **Agent:** <Agent>
- **Übernommen ab Head-SHA:** <sha> (stimmt mit Übergabe überein: ja/nein)
- **`tools/pruefen.sh`:** <Ergebnis>
**Bearbeiter:** <Agent>
```
