# 015 – Pushmeldingen

- **Soort:** feature
- **Status:** bezig (gebouwd en gecommit, nog niet gedeployed)
- **Prioriteit:** midden
- **Moeite:** M (eerste stap: enkel de daily-herinnering)
- **Aangemaakt:** 2026-10-22
- **Bron:** agent ideeen-brainstormer, gekozen door de gebruiker

## Waarom

De sterkste hefboom om spelers te laten terugkeren is een melding op het juiste moment: "De daily staat klaar", "Jan haalde je in", "Je reeks loopt vanavond af", "Er staat een partijtje open". Nu hangt alles af van wie eraan denkt om de app te openen.

## Wat (eerste stap: daily-herinnering)

1. **Web push** via de bestaande PWA en service worker. Geen e-mail.
2. Om een **vast uur voor iedereen** (voorstel: 19u Belgische tijd) krijgt elke speler die **vandaag nog niet speelde** één melding, bv. "⚽ De daily van vandaag wacht nog op je" (met de reeks erbij als die loopt).
3. Maximaal **één herinnering per dag** per speler, ook met meerdere toestellen niet meer dan één ronde.
4. De voorkeur staat **standaard aan**; in **Instellingen** kan de speler ze uitzetten.
5. Een tik op de melding opent de app op de daily.

### "Standaard aan" en de toestemming van de browser

Een site mag pas meldingen sturen nadat de speler in de browser "Toestaan" tikte, en die vraag mag enkel na een tik van de speler komen. "Standaard aan" betekent daarom:

- de voorkeur staat aan voor iedereen (ook bestaande spelers);
- zolang het toestel nog geen toestemming gaf, toont Home een kaartje **"Mis geen daily meer – zet meldingen aan"** met een knop die de toestemming vraagt; wegklikken verbergt het kaartje een tijd;
- op een iPhone in de browser zegt het kaartje eerst: "Zet Truitje Trek op je beginscherm" (web push werkt daar enkel als geïnstalleerde app, iOS 16.4+);
- weigert de speler in de browser, dan tonen we het kaartje niet meer en legt Instellingen uit hoe je het later toch toelaat.

Dit item gaat alleen over de daily-herinnering. Andere soorten meldingen (iemand haalde je in, partijtje of uitdaging open, special begint) kunnen later op dezelfde basis, elk met een eigen schakelaar.

## Beslist

- 2026-10-26: kanaal web push; vast uur voor iedereen; iedereen die vandaag nog niet speelde; standaard aan, uit te zetten in Instellingen; nu bouwen, enkel de daily-melding.

## Open vragen

1. ~~**Uur:**~~ beslist: 19u (tot 23u, daarna geen herinnering meer).
2. **Andere soorten** (klassement, partijtjes, specials): later apart beslissen, ook de privacyvraag over namen van andere spelers in een melding.

## Aanpak

- **Data:** tabel `PushSubscription` (gebruiker, endpoint, p256dh, auth, aangemaakt, laatst gebruikt; één rij per toestel) en op de gebruiker `DailyReminder` (standaard `true`) en `LastDailyReminder` (datum). Migratie via de skill `ef-migratie`.
- **Shared:** het uur en de regel "wie krijgt vandaag een herinnering" in `GameRules`, met tests op de verzetbare klok.
- **Server:**
  - NuGet-pakket WebPush; VAPID-sleutels als app-instellingen op Azure (`Push__PublicKey`, `Push__PrivateKey`, `Push__Subject`), nooit in git en nooit wijzigen (anders vallen alle abonnementen weg).
  - Endpoints: publieke sleutel ophalen, abonnement registreren, abonnement verwijderen, voorkeur aan/uit.
  - `DailyReminderService` (BackgroundService, zoals `MatchEngine`) kijkt elke ~10 minuten: uur voorbij, voorkeur aan, vandaag niet gespeeld, vandaag nog geen herinnering → versturen naar alle toestellen, `LastDailyReminder` zetten. Werkt omdat er precies één instantie draait met Always On.
  - Antwoordt de pushdienst 404/410, dan wordt dat abonnement verwijderd.
- **Client:** JS-interop om te abonneren; `push`- en `notificationclick`-listener in `service-worker.js` én `service-worker.published.js`; kaartje op Home; schakelaar in Instellingen.
- **Tests:** herinnering enkel voor wie de voorkeur aan heeft en vandaag niet speelde, nooit twee keer per dag, niet vóór het uur, verlopen abonnement wordt opgeruimd.
- **Testen op een toestel:** enkel via HTTPS of localhost; op iPhone enkel als geïnstalleerde app.

## Notities

- 2026-10-22: gekozen uit de brainstorm samen met [013](013-daag-uit.md), [014](014-verzamelalbum.md) en [016](016-revanche-online-wedstrijd.md).
- Eerlijke waarschuwing uit de brainstorm: het onderhoud is groter dan het lijkt, en op iPhone werkt het enkel na installatie op het beginscherm.
- 2026-10-26: naar voren gehaald (was "januari?" in de [planning van 23/10](planning-2026-10-23.md)), beperkt tot de daily-herinnering.
- 2026-10-26: gebouwd. Vast uur 19u, gestuurd tot 23u. Op Home een kaartje bovenaan; schakelaar in de spelersfiche (tab Account); afmelden koppelt het toestel los; account verwijderen wist de toestellen. Enkel pushdiensten van Google, Mozilla, Apple en Microsoft. Sleutels: `scripts/vapid-keys.ps1`, uitleg in de README onder "Pushmeldingen". Nog te doen vóór het live gaat: VAPID-sleutels op Azure zetten en één keer echt testen op een Android-gsm en een iPhone (beginscherm).
