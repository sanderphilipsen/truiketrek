# 032 – Een vraag betwisten vanuit de daily

- **Soort:** feature
- **Status:** concept
- **Prioriteit:** midden
- **Moeite:** M
- **Aangemaakt:** 2026-10-25
- **Bron:** eigen idee

## Waarom

Fouten in een vraag ziet de speler meestal eerst: "maar Mertens speelde toch ook bij PSV?", "die tip klopt niet", "mijn antwoord is hetzelfde, enkel anders geschreven". Vandaag kan hij dat enkel kwijt via de algemene feedbackpagina (`Feedback.razor`, enkel Bug of Idee, los van een vraag), en dan moet de admin zelf uitzoeken over welke dag en vraag het gaat en wat de speler antwoordde. Daardoor wordt er weinig gemeld en blijft een foute vraag staan.

Een knop **Betwisten** bij de vraag zelf maakt melden laagdrempelig en geeft de admin meteen alles wat nodig is om een correctie te doen ([031](031-correctie-gespeelde-dag.md)).

## Wat

### Voor de speler

- Na het beantwoorden van een vraag, op het moment dat het juiste antwoord getoond wordt, staat een kleine knop **Betwisten** (ook bij het dagresultaat, per vraag). Nooit tijdens het spelen: anders wordt betwisten een manier om de tijd stil te zetten of een hint te vissen.
- Een kort formulier:
  - **Reden** (kies één): *mijn antwoord is ook juist* · *het juiste antwoord klopt niet* · *een tip of de vraag klopt niet* · *iets anders*.
  - **Uitleg** (optioneel, kort), bv. "Hij speelde van 2004 tot 2006 bij PSV".
  - Het eigen antwoord wordt automatisch meegestuurd (`LastGuess`, `Choice`, ...), de speler hoeft het niet over te typen.
- Eén betwisting per vraag per speler. Daarna toont de knop "Betwist, in behandeling".
- Als de admin beslist, krijgt de speler een melding (`Notification`): *Aanvaard: vraag 4 werd geneutraliseerd, je kreeg er 80 punten bij* of *Afgewezen* met een korte uitleg.

### Voor de admin

- Een lijst **Betwistingen**, gegroepeerd per vraag: dag, vraag, het juiste antwoord, hoeveel spelers betwisten, hun redenen, uitleg en hun antwoord, en hoeveel van alle spelers de vraag juist hadden.
- Vanuit de betwisting rechtstreeks naar de **correctie** van 031 (neutraliseren of schrappen). Beslist de admin, dan krijgen alle betwisters van die vraag tegelijk hun antwoord.
- Een melding voor de admin bij de eerste betwisting op een vraag (niet bij elke volgende), zoals nu bij een nieuwe bug.

## Open vragen

- **Ziet de speler dat anderen betwisten?** Bv. "3 spelers betwisten deze vraag". Geeft vertrouwen dat er iets mee gebeurt, maar kan ook een kudde-effect geven. Voorstel: nee in fase 1.
- **Hoe lang mag je betwisten?** Enkel de dag zelf, of ook nog een paar dagen erna (bv. tot het einde van de week)? Voorstel: tot 7 dagen na de daily, want na een afgesloten maand wordt een correctie lastig (Gouden Bal).
- **Ook in specials, training en online?** Specials ja (zelfde scores). Training: wel melden, maar zonder punten. Online: later, of enkel als melding zonder correctie.
- **Beloning?** Een kleine erkenning voor wie een terechte fout vond (bv. badge "VAR" na 3 aanvaarde betwistingen)? Risico: mensen gaan alles betwisten. Voorstel: badge enkel op aanvaarde betwistingen, en niet in fase 1.
- **Misbruik:** een maximum per dag (bv. 3 betwistingen), en een admin kan iemand die blijft spammen de knop ontnemen?
- **Ook als het juist was?** Mag je een vraag betwisten die je juist had (bv. "de tip verraadde het antwoord")? Voorstel: ja, dat is nuttige info, maar het levert nooit punten op.
- **Bestaande feedback:** een nieuw `FeedbackType.Betwisting` in de bestaande feedback, of een aparte entiteit? Voorstel: aparte entiteit, want ze hangt aan een vraag en een poging en heeft een eigen afhandeling; wel dezelfde meldingen.

## Aanpak (optioneel)

- **Data (migratie):** entiteit `QuestionDispute` (gebruiker, `DailyQuestionId`, `AttemptId`, reden, uitleg, tijdstip, status open/aanvaard/afgewezen, antwoord van de admin, verwijzing naar de `ScoreCorrection` van 031). Unieke index op gebruiker + vraag.
- **Shared:** reden-enum, DTO's, de regels (termijn, maximum per dag) in Shared.
- **Server:** `DisputeService` (indienen, enkel na een beantwoorde poging en binnen de termijn; lijst voor admin; afhandelen met melding aan alle betwisters). Antwoord van de speler server-side uit de poging halen, niet uit de request.
- **Client:** knop en formulier in `Question.razor` (na het tonen van het antwoord) en bij het dagresultaat; admin-pagina Betwistingen (of een tab bij `AdminFeedbackPage`).
- **Tests:** betwisten kan niet vóór het antwoord, één per vraag, termijn, afhandeling stuurt meldingen.
- **Review:** spelregel-reviewer (geen lek van het antwoord, niets op de client beslist) en security-wachter (spam, andermans pogingen).
- **Volgorde:** kan los van 031 gebouwd worden (de admin handelt dan af met een antwoord), maar is pas echt nuttig samen met de correctie.

## Notities

- Verwant: [031](031-correctie-gespeelde-dag.md) (de correctie zelf).
