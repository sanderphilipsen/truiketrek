# 036 – Schaakklok: om beurten een naam noemen (online)

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-26
- **Bron:** brainstorm nieuwe vraagtypes (2026-10-26)

## Waarom

Online speelt nu iedereen naast elkaar dezelfde vragen. Schaakklok maakt er een echt duel van: je speelt om beurten, en elke naam die je noemt is er een minder voor je tegenstander. De spanning loopt vanzelf op, want de makkelijke namen zijn snel weg en daarna moet je echt diep graven. Het lijkt op [Lijstdag](018-lijstdag.md), maar daar zoek je alleen.

## Wat

- Twee spelers krijgen één opdracht, bijvoorbeeld **"Noem een Rode Duivel"**, **"Noem een speler van Anderlecht"** of **"Noem een WK-finalist"**.
- Elke speler heeft een eigen klok, bv. 60 seconden in totaal. Je klok loopt alleen tijdens jouw beurt, zoals bij schaken.
- Je typt een naam. Een geldige naam die nog niet genoemd is, zet je klok stil en geeft de beurt door. Een ongeldige naam of een naam die al viel, kost tijd, en de beurt blijft bij jou.
- Je mag passen, maar dat kost tijd, bv. 5 seconden.
- Wie door zijn tijd heen is, verliest. Daarna ziet iedereen de genoemde namen en een paar die niemand vond.

## Open vragen

- **Alleen online, of ook een solo-versie** tegen een computertegenstander die zijn namen uit de lijst haalt, met vertraging en af en toe een fout?
- **Punten:** alleen winst of verlies, of punten per genoemde naam? Hoe telt het mee voor de online rating?
- **Klok:** hoeveel tijd per speler, krijg je tijd terug per juiste naam (zoals bij schaken met increment), en hoeveel kost een fout of een pas?
- **Lijsten:** gebruiken we de lijsten van Lijst, of komen er aparte, langere lijsten? Een opdracht moet genoeg namen hebben (minstens 40?), anders raakt de lijst op.
- **Lijst op:** wat als alle namen genoemd zijn? Gelijkspel, of wint wie de meeste tijd overhoudt?
- **Spelfouten en bijnamen:** hoe streng is AnswerMatcher hier? Een twijfelgeval mag geen beurt kosten.

## Aanpak (optioneel)

- **Shared:** klokregels (starttijd, straf, eventuele increment) in `QuestionRules`.
- **Server:** de klok loopt op de server. De server houdt de beurt, de resterende tijd en de genoemde namen bij en controleert elke naam. De client toont enkel de klok en krijgt de volledige lijst pas na afloop, zodat er geen antwoorden lekken (zie `NoLeakTests`).
- **SignalR:** beurtwissel en klokstand naar beide spelers sturen, en rekening houden met vertraging (`LatencyGraceSeconds`).
- **Client:** twee klokken boven elkaar, de actieve oplichtend, met de genoemde namen als lijst die groeit.
- Volg de skill `nieuw-vraagtype`, ook al is dit eerder een online modus dan een daily-vraag.

## Notities

- 2026-10-26: toegevoegd na de brainstorm. Ook genoemd maar (nog) niet op de backlog gezet: Speel de match, Kettingreactie, Wachtwoord, Bluf, Weekraadsel, Logigram, Wie is het?
