---
name: ef-migratie
description: Maakt een EF Core-migratie aan na een wijziging aan het datamodel (Data/Entities.cs of QuizDbContext) van Truitje Trek en controleert ze. Gebruik dit telkens wanneer entiteiten, kolommen, indexen of relaties wijzigen, of wanneer de gebruiker om een migratie vraagt.
---

# EF Core-migratie

Het datamodel staat in `src/VoetbalQuiz.Server/Data/Entities.cs` en `QuizDbContext.cs`, de migraties in `src/VoetbalQuiz.Server/Data/Migrations`. De server voert de migraties zelf uit bij het opstarten, ook op Azure. Een fout hierin legt dus productie stil.

## Stappen

1. **Tool klaarzetten** (dotnet-ef staat in `dotnet-tools.json`):
   ```bash
   dotnet tool restore
   ```
2. **Migratie aanmaken** met een korte PascalCase-naam die de wijziging beschrijft (bv. `PlayerNicknames`, `SpecialEndDate`):
   ```bash
   dotnet ef migrations add <Naam> --project src/VoetbalQuiz.Server --output-dir Data/Migrations
   ```
3. **Nalezen.** Open de nieuwe `<timestamp>_<Naam>.cs`:
   - Doet `Up` enkel wat je bedoelde? Geen onverwachte `DropColumn`/`DropTable` (dat is dataverlies op productie).
   - Een hernoemde property wordt door EF vaak als drop + add gezien: vervang dat door `RenameColumn`.
   - Een nieuwe verplichte kolom op een bestaande tabel heeft een `defaultValue`.
   - `Down` draait `Up` correct terug.
   - `QuizDbContextModelSnapshot.cs` is mee aangepast.
4. **Toepassen en testen:** start de database (`pwsh scripts/start-db.ps1`) en de app; die migreert bij het opstarten. Draai daarna `dotnet test`.

## Ongedaan maken

Nog niet toegepast of gedeployed:
```bash
dotnet ef migrations remove --project src/VoetbalQuiz.Server
```
Al gedeployed naar Azure: nooit een bestaande migratie aanpassen of verwijderen, maak een nieuwe die het rechtzet.
