# Homescherm: herontwerp (concept, 2026-10-19)

Bronnen: UX-review van de ux-reviewer en conceptvoorstel van de ux-designer (19/10), plus de keuzes van de product owner.

## Keuzes van de product owner
- **Rol van Home:** de daily eerst. Home draait om de dagelijkse gewoonte.
- **Specials:** op de startdag van een special staat die special bovenaan, boven de daily. Op de andere dagen staat ze onder de daily.
- **Online-blok:** compact, met maximaal 3 open partijtjes en "Alles ›" naar `/online`. Het aanmaakformulier blijft op Home.
- **Gouden Bal:** een compacte strook die openklapt, onder de daily.
- **Motivatie:** de klassementspositie van vandaag. Als je gestegen bent, licht die kort op ("▲ 3 plaatsen").
- **Nieuwe spelers:** een kaart "Zo werkt het" boven de daily, die je kan wegklikken. Ze verdwijnt ook na je eerste daily.

## Opbouw van boven naar onder
1. Topbar. De grote begroeting verdwijnt of wordt één regel muted tekst.
2. De banner "lopend partijtje", enkel als je in een partijtje zit.
3. "Zo werkt het", enkel voor nieuwe spelers en wegklikbaar.
4. De hero, afhankelijk van de toestand:
   - startdag van een special: het special-kaartje ("nieuw vandaag") bovenaan met een gewone knop "Speel", geen grote hero-knop;
   - anders: de daily ("Start de daily" of "Verder met vraag N");
   - daily klaar: score, klassementspositie (▲ als je gestegen bent) en een volgende stap.
5. De strook Gouden Bal, enkel op de aankondigingsdag.
6. De lopende special (er loopt er altijd maar één tegelijk) als kaart over de volle breedte, zonder sectiekop "Specials".
7. Online spelen, compact: maximaal 3 open partijtjes, het formulier "Nieuw partijtje" en een vriendelijke lege toestand.
8. "Binnenkort": de eerstvolgende 3 aankomende specials als compacte kaartjes in een veegrij (icoon, naam, "over N dagen").

## Overal toe te passen
- **Eén blikvanger:** er is maar één grote gele hoofdknop per scherm, plus de banner van het partijtje.
- **Skeletons met een zachte shimmer** in plaats van spinners, met een vaste `min-height` per blok, zodat het scherm niet springt. Daily, specials en Gouden Bal laden parallel, en een fout raakt alleen het eigen blok, met een knop "Opnieuw proberen". De shimmer valt weg bij `prefers-reduced-motion`.
- **Eén sectiekop-component:** de titel links en "Alles ›" rechts, met een tikvlak van minstens 44 px. Dit vervangt "Overzicht ›", "Lobby ›" en de losse kop "Open partijtjes".
- **Quick wins uit de review:**
  - tikvlakken van minstens 44 px (card-link, Join, header);
  - tabbar-labels en level-pill van minstens 12 px;
  - `aria-label` en `aria-current` op de tabbar;
  - de voortgang met ✓/✗ en een `aria-label`;
  - "Verder met vraag N" zodra een vraag geopend is;
  - een ondertitel bij "Online spelen".

## Vraagt server- of API-werk
- De klassementspositie van vandaag en de positie bij het vorige bezoek, nodig voor "▲ N plaatsen". Dit hoort in `DailyOverview` of in een apart endpoint, en de positie wordt op de server berekend.
- "Startdag van een special" bepaalt de server (start van de special, in de tijdzone Brussel).
- "Verder met vraag N" na het openen van een vraag: nagaan of de server die toestand al bijhoudt.
- "Zo werkt het verborgen": lokaal bijhouden volstaat, of als vlag op de speler.

## Taakverdeling
- **ux-designer:** opbouw, componenten, CSS en quick wins.
- **animatie-bouwer:** skeleton-shimmer, het oplichten van ▲ en de veegrij.
- **spelregel-reviewer:** de wijzigingen aan klassement en API nakijken.
- **mobiel-tester:** alle toestanden testen op 375 px.
