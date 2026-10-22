# 015 – Pushmeldingen

- **Soort:** feature
- **Status:** idee
- **Prioriteit:** laag
- **Moeite:** L
- **Aangemaakt:** 2026-10-22
- **Bron:** agent ideeen-brainstormer, gekozen door de gebruiker

## Waarom

De sterkste hefboom om spelers te laten terugkeren is een melding op het juiste moment: "De daily staat klaar", "Jan haalde je in", "Je reeks loopt vanavond af", "Er staat een partijtje open". Nu hangt alles af van wie eraan denkt om de app te openen.

## Wat

1. In **Instellingen** een blok **Meldingen** met een hoofdschakelaar en per soort een eigen schakelaar:
   - de daily staat klaar (vast uur, bv. 8u);
   - herinnering als je om 20u nog niet speelde en je reeks dreigt te breken;
   - iemand haalde je in op het maandklassement;
   - er staat een online partijtje of een uitdaging ([013](013-daag-uit.md)) open voor jou;
   - een special begint.
2. Alles staat **standaard uit**; de speler zet aan wat hij wil. Bij het aanzetten vraagt de browser om toestemming.
3. Een melding opent de app op de juiste pagina (daily, partijtje, klassement).

## Open vragen

1. **iPhone:** web push werkt daar alleen als de app op het beginscherm geïnstalleerd is (iOS 16.4+). Tonen we een uitleg "Installeer de app om meldingen te krijgen"? Is het de moeite als een groot deel van de spelers iPhone heeft?
2. **Tijdstippen:** vast uur voor de daily-melding, of kiest de speler het uur?
3. **Frequentie:** maximaal één melding per dag buiten de online partijtjes? Niemand wil spam van een quiz-app.
4. **"Jan haalde je in":** alleen bij directe buren in het klassement, of ook bij top 3? Hoe vaak mag dit per dag?
5. **Privacy:** een melding bevat een naam van een andere speler. Is dat oké voor iedereen, of alleen voor spelers die zichtbaar zijn in het klassement?
6. **Onderhoud:** VAPID-sleutels beheren, verlopen abonnementen opruimen, en meerdere toestellen per gebruiker. Wie houdt dat in de gaten?

## Aanpak (optioneel)

- **Data:** tabel `PushSubscription` (gebruiker, endpoint, sleutels, toestel, aangemaakt) en voorkeuren per soort op de gebruiker. Migratie via de skill `ef-migratie`.
- **Server:** VAPID-sleutels in de configuratie; een `PushService` dat meldingen verstuurt (bv. met de bibliotheek WebPush); achtergrondtaken sluiten aan op bestaande momenten: dagstart, dagafsluiting, `MatchEngine` (partijtje open), maandklassement.
- **Client:** de PWA en de service worker bestaan al; uitbreiden met een `push`-listener en `notificationclick`. Schakelaars in de instellingenpagina.
- **Tests:** per soort een test dat de melding alleen gaat naar wie ze aanzette, en nooit meer dan het maximum per dag.

## Notities

- 2026-10-22: gekozen uit de brainstorm samen met [013](013-daag-uit.md), [014](014-verzamelalbum.md) en [016](016-revanche-online-wedstrijd.md).
- Eerlijke waarschuwing uit de brainstorm: het onderhoud is groter dan het lijkt, en op iPhone werkt het enkel na installatie op het beginscherm.
