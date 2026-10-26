---
name: nieuw-vraagtype
description: Checklist om een nieuw vraagtype toe te voegen aan Truitje Trek (daily, specials, online, admin, badges, tests). Gebruik dit wanneer de gebruiker een nieuw soort vraag of ronde wil, of een bestaand type grondig wil wijzigen.
---

# Nieuw vraagtype

Een vraagtype raakt veel plekken. Zoek eerst hoe een bestaand type met dezelfde vorm het doet en volg dat patroon:

- typt een naam met tips die verschijnen → `Wissel` of `Rugnummer` (vragenbank) of `Kluts` (uit de spelers)
- kiest een optie → `Snelvuur` of `NietBij`
- meerdere antwoorden in één vraag → `Opstelling` of `Counter`

`grep -rn "QuestionType.Wissel" src tests` toont alle plekken voor dat type. Werk ze één voor één af.

## Checklist

**Shared** (`src/VoetbalQuiz.Shared`)
- [ ] `QuestionType`-enum: nieuwe waarde achteraan. De naam wordt als tekst in de database bewaard (`HasConversion<string>()`), dus hernoem nooit een bestaande waarde.
- [ ] `QuestionTypes.All`: naam, icoon, uitleg, `PerDay`, `MaxPoints`, bestandsnaam of `FromPlayers: true`.
- [ ] Een `<Type>Data`-record als het uit de vragenbank komt.
- [ ] Tijden en punten als constanten in `QuestionRules` (geen magic numbers elders).
- [ ] Controleer `Mix`: een dag blijft ≤ 10 vragen en ≤ `DayMaxPoints`.

**Server** (`src/VoetbalQuiz.Server`)
- [ ] `QuestionEngine`: antwoordtekst, tijdslimiet, wat de client te zien krijgt per moment, beoordeling en punten. Enkel vrijgegeven info naar de client; het antwoord pas na afloop.
- [ ] `QuestionSeeder.Normalize`: inlezen en sleutel bepalen (vragenbank).
- [ ] `QuizService` (daily/specials) en `MatchService` (online) als het type iets speciaals vraagt.
- [ ] `AdminService` + DTO's in `AdminDtos.cs` voor het bewerken en de voorraad.
- [ ] Migratie als het datamodel wijzigt (skill `ef-migratie`).
- [ ] `Data/questions/<bestand>.json` met een paar vragen (skill `vraag-toevoegen`).

**Client** (`src/VoetbalQuiz.Client`)
- [ ] `Shared/QuestionPlay.razor` en `QuestionDisplay.cs`: tonen tijdens het spelen.
- [ ] `Shared/QuestionAnswer.razor`: het antwoord nadien.
- [ ] `Pages/Question.razor` en `Pages/Wedstrijd.razor` (online).
- [ ] Admin: `AdminVraag.razor`/`AdminVragen.razor`, en `AdminSpecial.razor` als specials het type moeten voorstellen.
- [ ] CSS in `wwwroot/css/screens/vraagtypes.css` (of een eigen bestand in `screens/`, dan ook in `index.html` opnemen), getest op 375 px breed.

**Rest**
- [ ] Badges per vraagtype, als dat past.
- [ ] Tests in `tests/VoetbalQuiz.Tests` (`QuestionTypeTests.cs`, `NewRoundTests.cs`): punten, tijd, beoordeling, mix-budget.
- [ ] README: de tabel met vraagtypes.

## Afronden

1. `dotnet build` en `dotnet test`.
2. Laat de agent `spelregel-reviewer` de wijziging nakijken.
3. Laat de agent `mobiel-tester` het type spelen in de daily en online.
