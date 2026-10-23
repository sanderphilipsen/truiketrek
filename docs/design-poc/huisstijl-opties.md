# Truitje Trek: zes huisstijlopties

Concept, geen productiecode. Preview met telefoonframes (375 px, per optie home, vraag, beloning en klassement): [`huisstijl-opties.html`](huisstijl-opties.html).

## Vertrekpunt

De huidige stijl is een donker scorebord: groen veld (`--pitch` #0b3d24 naar `--pitch-2` #0f5132), donkere tegels (`--card` #06291b) met een dunne groene lijn (`--sb-line`), gele monospace-cijfers (`--sb-digit` #fac775), system-ui en `--radius: 12px`. Het geheel is "dark first": `:root` is donker en `.surface-light` zet de tokens terug op licht voor admin, de beloningspopup en invoervelden. Achter alles loopt het levende veld (`body::before` plus `PitchFx`/`pitch.js`, met een nachtvariant).

In `docs/design-poc/` staan al elf verkenningen, bijna allemaal sterk thematisch (stickeralbum, teletekst, krijtbord, stadionbord, kantine, ...). De zes opties hieronder zijn bewust **huisstijlen** en geen metaforen: ze werken op elk scherm, ook op admin en formulieren, zonder dat alles een sticker of krijtlijn moet worden.

| # | Optie | Licht/donker | Temperatuur | Koppen |
|---|---|---|---|---|
| 1 | Speelplein | licht | fris (blauw, zon) | rond display |
| 2 | Perskamer | licht | neutraal (inkt, kobalt) | serif |
| 3 | Driekleur | licht | heet (zwart, geel, rood) | zware poster-grotesk |
| 4 | Nachtmatch | donker | koud (inktblauw, neon) | breed geometrisch |
| 5 | Ereloge | donker | warm (bordeaux, goud) | condensed kapitalen |
| 6 | Mundial '86 | licht | aards-warm (oker, terracotta, petrol) | retro-rond |

Alle contrastwaarden zijn berekend met de WCAG-formule. Tekst is overal ≥ 4,5:1, randen van tikvlakken ≥ 3:1. Juist en fout hebben altijd een icoon (vinkje of kruis) en een tekstlabel ("Juist", "Jouw keuze"), dus nooit enkel kleur.

---

## 1 · Speelplein

**Gevoel:** zaterdagvoormiddag op het jeugdveld: vriendelijk, rond en een tikje uitbundig.
**Doelgroep/sfeer:** brede groep, ook occasionele spelers en jongeren. Duolingo-achtige spelbeleving.

| Rol | Hex | Contrast |
|---|---|---|
| Achtergrond | #f2f7ff | |
| Oppervlak | #ffffff | |
| Tekst | #1b2a41 | 14,4 op wit, 13,4 op achtergrond |
| Rustige tekst | #52627a | 6,2 op wit |
| Accent (primair) | #1a6fd1 | witte tekst 4,95 |
| Accent 2 (zon, XP) | #ffc93c | tekstkleur erop 9,4 |
| Juist | #23863a | witte tekst 4,6; tekst #1d6f31 op #e3f7e7: 5,6 |
| Fout | #d9363e | witte tekst 4,6; tekst #b4232b op #fdeaea: 5,6 |
| Rand tikvlak | #7489ab | 3,6 op wit |

- **Typografie:** Baloo 2 800 voor koppen en cijfers, Nunito 600 tot 800 voor tekst.
- **Vormtaal:** radius 16 tot 24 px, kaarten met 2 px zachte rand en een "voetje" (`0 3px 0`), knoppen in "3D" (`0 4px 0` donkerdere tint) die bij een tik indrukken. Iconen met 2,6 px lijn, afgerond. Een truitje-mascotte als gezicht van de app.
- **Beweging:** veerkrachtig (overshoot-easing): juist poppt op, fout schudt kort, de feedback schuift als bottom sheet omhoog, de vlam van de reeks wiebelt, confetti bij de beloning.
- **Kernmomenten:** juist = groen vlak met vinkje en "Juist"; fout = zachtrode knop met kruis en "Jouw keuze", plus een sheet "Naast! Simon Mignolet was juist. De volgende zit erin." met een neutrale blauwe knop (fout is nooit rood-alarm). Reeks = oranje vlam met groot getal. Beloning = mascotte, "+86 XP" op een zonnige blok, XP-onderdelen, levelbalk en badge "Op dreef". Klassement = podium met avatars plus lijst, jouw rij blauw omrand.
- **Risico's:** kan kinderlijk ogen voor de doorgewinterde supporter; de dikke knoppen en voetjes vragen meer verticale ruimte (minder past boven de vouw); mascotte en veel kleur vragen discipline zodat niet elk scherm een kermis wordt.
- **Migratie:** middel. Lichte modus wordt de basis (`.surface-light` kan grotendeels opgaan in `:root`), het levende veld (`body::before`, `PitchFx`) verdwijnt of wordt een rustige achtergrond. Nieuwe tokens: `--font-head`, `--font-body`, `--btn-depth`, `--card-foot`. De bestaande rolnamen (`--accent`, `--ok`, `--fail`, `--card`) blijven bruikbaar.

## 2 · Perskamer

**Gevoel:** de rust van het sportkatern op zondagochtend: grote cijfers, weinig ruis.
**Doelgroep/sfeer:** volwassen, kenners, "premium". Denk aan FotMob en Apple.

| Rol | Hex | Contrast |
|---|---|---|
| Achtergrond | #f4f4f1 | |
| Oppervlak | #ffffff | |
| Tekst | #111111 | 17,1 op achtergrond |
| Rustige tekst | #5c5c57 | 6,7 op wit, 6,1 op achtergrond |
| Accent | #2b3ff5 | 6,7 op wit; op #eceeff 5,8 |
| Juist | #0b7a3b | 5,4 op wit, 4,9 op #e9f6ee |
| Fout | #c8231b | 5,7 op wit, 4,9 op #fcecea |
| Haarlijn | #e3e3de | decoratief |
| Rand tikvlak | #8a8a84 | 3,5 op wit |

- **Typografie:** Instrument Serif voor koppen (ook cursief voor "Naast!"), Inter voor tekst, Inter Tight 600 met `tabular-nums` voor cijfers.
- **Vormtaal:** radius 12 tot 14 px, geen randen maar haarlijnen en lijsten, nauwelijks schaduw. Zwarte hoofdknop, kobalt enkel voor voortgang en "jij". Iconen 1,75 px lijn.
- **Beweging:** kort en functioneel: fade-up, de tijdlijn krimpt lineair, balken lopen vol. Geen confetti; de beloning zit in typografie (een "+86" van 112 px).
- **Kernmomenten:** juist = lichtgroene rij met lijn links, vinkje en "Juist"; fout = lichtrode rij met kruis en "Jouw keuze"; daaronder een cursieve "Naast!" met de uitleg. Reeks = groot cijfer in een KPI-rij. Beloning = XP als krantenkop met een tabel eronder. Klassement = echte tabel met kolommen, trendpijlen en jouw rij in kobaltwaas.
- **Risico's:** te koel voor een spel; plezier moet uit copy en beweging komen. Serif-koppen vragen een goed geladen webfont (anders valt het terug op Georgia). Gelijkt op veel sport-apps, dus minder eigen smoel.
- **Migratie:** middel tot laag. Het minst aantal nieuwe componenten; vooral tokens, typografie en het weghalen van kaders/veld. Nieuwe tokens: `--font-head`, `--font-num`, `--hair`.

## 3 · Driekleur

**Gevoel:** een supportersaffiche van de Rode Duivels: luid, trots en met de mouwen opgestroopt.
**Doelgroep/sfeer:** fans die Belgisch voetbal willen voelen; uitgesproken en herkenbaar.

| Rol | Hex | Contrast |
|---|---|---|
| Achtergrond | #fff3d1 (met rasterpuntjes) | |
| Oppervlak | #ffffff | |
| Tekst (inkt) | #141414 | 16,7 op achtergrond, 12,6 op geel |
| Rustige tekst | #4d4434 | 8,7 op achtergrond |
| Accent geel | #ffd100 | inkt erop 12,6; geel op inkt 12,6 |
| Accent rood | #d8001e | witte tekst 5,3; rode tekst #c4001b op wit 6,2 |
| Juist | #0a7f45 | witte tekst 5,1 |
| Fout | #d8001e | witte tekst 5,3 |
| Rand | #141414, 3 px | 16,7 |

- **Typografie:** Archivo Black in kapitalen voor koppen, Space Grotesk 500/700 voor tekst, Space Mono 700 voor cijfers.
- **Vormtaal:** radius 4 px, 3 px zwarte randen, harde schaduw `4px 4px 0` die bij een tik verdwijnt (knop "zakt" in het papier). Vlag-strook bovenaan, stickers die scheef staan, een rode ster-burst voor XP. Iconen 3 px, vierkante uiteinden.
- **Beweging:** stempelend en kort: stickers slaan neer, de XP-burst wordt gestempeld, fout schokt in twee stappen (geen vloeiende wiebel).
- **Kernmomenten:** juist = groen vlak met zwart letterblok en "JUIST"; fout = rood vlak met "JOUW KEUZE"; zwarte balk "NAAST!" in geel met "+0". Reeks = gele sticker. Beloning = gele pagina met burst "+86 XP", kassabon met XP en sticker-badge. Klassement = blokken met cijfer in zwart vak, jouw rij rood.
- **Risico's:** rood is merk én fout: een fout antwoord oogt dan als "de huisstijl", en omgekeerd voelt rood overal een beetje als fout. Vermoeiend bij lang spelen en moeilijk rustig te krijgen voor admin. Schuurt met de eerdere POC's stickeralbum en sjaal-tifo.
- **Migratie:** hoog. Niet enkel tokens: elke component krijgt randen, harde schaduwen en kapitalen; spacing moet ruimer voor de schaduwen; het rood/fout-conflict vraagt een aparte foutvorm.

## 4 · Nachtmatch

**Gevoel:** woensdagavond, Champions League, lichtmasten aan: koel, snel en elektrisch.
**Doelgroep/sfeer:** fanatieke en jongere spelers, online partijtjes 's avonds.

| Rol | Hex | Contrast |
|---|---|---|
| Achtergrond | #0b0d1f (met limoen- en magentagloed) | |
| Oppervlak | #171b3a | |
| Tekst | #eef0ff | 17,0 op achtergrond, 14,8 op oppervlak |
| Rustige tekst | #a3a9d1 | 7,3 op oppervlak |
| Accent limoen | #c6ff3d | achtergrondkleur erop 16,3; als tekst 14,2 |
| Accent 2 magenta | #ff5fdc | 6,3 op oppervlak |
| Juist | #4ee08f | 9,9 op oppervlak |
| Fout | #ff7686 | 6,5 op oppervlak |
| Rand tikvlak | #6a74ab | 3,75 op oppervlak |

- **Typografie:** Unbounded 700 voor koppen, Manrope 500 tot 800 voor tekst, Chakra Petch 600 voor cijfers.
- **Vormtaal:** radius 18 px, glas-tegels met 1 px lichte rand, de daily met een verloopende rand van limoen naar magenta, pil-knoppen met neon gloed. Iconen 2 px.
- **Beweging:** gloed die aanzwelt bij juist, timer-ring die leegloopt, lichtstralen die langzaam draaien achter de XP.
- **Kernmomenten:** juist = groene omlijning met gloed, vinkje en "Juist"; fout = koraalrode omlijning, kruis en "Jouw keuze". Reeks = magenta vlam. Beloning = "+86 XP" in limoen met stralenkrans. Klassement = glas-rijen, top 3 in limoen, jouw rij met limoenrand en gloed.
- **Risico's:** blijft donker, dus het verschil met nu is kleiner dan bij de lichte opties; gloed en verlopen kosten GPU en batterij op oudere gsm's; snel "gamer-app" en minder Vlaams-gezellig.
- **Migratie:** laag tot middel. De token-structuur is al donker; vooral kleurwaarden, fonts, `--radius` en een `--glow` erbij. Het veld maakt plaats voor een rustige gloed.

## 5 · Ereloge

**Gevoel:** een avond in de ereloge met de Gouden Schoen op tafel: warm, plechtig en een tikje chic.
**Doelgroep/sfeer:** wie van prestige en tradities houdt: Gouden Bal, dagzege, maandwinnaar.

| Rol | Hex | Contrast |
|---|---|---|
| Achtergrond | #1a0d10 | |
| Oppervlak | #2a161b | |
| Tekst | #f6ece0 | 16,2 op achtergrond, 14,6 op oppervlak |
| Rustige tekst | #c7b29f | 8,4 op oppervlak |
| Accent goud | #d8b46a | 8,7 op oppervlak; donkere tekst erop 9,6 |
| Juist | #84d9a6 | 10,1 op oppervlak |
| Fout | #ff9585 | 8,1 op oppervlak |
| Rand tikvlak | #8a6f4a | 3,6 op oppervlak |

- **Typografie:** Oswald 500 in kapitalen met ruime spatiëring voor koppen, DM Sans voor tekst, Oswald 300 met `tabular-nums` voor grote cijfers.
- **Vormtaal:** radius 2 px, gouden haarlijnen, dubbele lijn als scheiding, sterren als ornament, ruitjes als voortgang. Gecentreerde composities. Iconen 1,5 px.
- **Beweging:** traag en waardig: de XP "landt" met krimpende spatiëring, badges faden in, gouden balken lopen vol. Geen confetti.
- **Kernmomenten:** juist = mintgroene dubbele rand, vinkje en "JUIST"; fout = koraalrand met kruis en "JOUW KEUZE"; gecentreerde "NAAST!". Reeks = groot dun cijfer. Beloning = trofee-icoon, "+86 XP" in goudverloop, grootboek met stippellijnen, medaille-badge. Klassement = tabelrijen met gouden nummer 1 en jouw rij met goudlijn.
- **Risico's:** plechtig en weinig speels; fout voelt sneller zwaar. Goud op bordeaux wordt snel kitsch als het te veel terugkomt. Donker, dus net als optie 4 een kleinere sprong.
- **Migratie:** laag tot middel. Zelfde donkere structuur; nieuwe kleuren, fonts, kleinere radius en enkele ornamentklassen.

## 6 · Mundial '86

**Gevoel:** Mexico '86 op een zomerse kermis: zon, retrostrepen en de warmte van een oud wedstrijdticket.
**Doelgroep/sfeer:** nostalgische fans (de Rode Duivels van '86), maar ook gewoon gezellig en licht.

| Rol | Hex | Contrast |
|---|---|---|
| Achtergrond | #f8eedd | |
| Oppervlak | #fffaf1 | |
| Tekst | #2b1d14 | 14,2 op achtergrond |
| Rustige tekst | #6b5444 | 6,1 op achtergrond, 6,8 op oppervlak |
| Accent petrol | #0f6e6a | witte tekst 6,1; als tekst 5,8 |
| Accent 2 oker | #e9a92b | tekst erop 7,9 |
| Accent 3 terracotta | #c4511d | witte tekst 4,6; als tekst op oppervlak 4,45, dus enkel voor grote cijfers (≥ 3:1) |
| Juist | #2f7d32 | witte tekst 5,1 |
| Fout | #b4372b | 5,7 op oppervlak, witte tekst 6,0 |
| Rand tikvlak | #8f7663 | 4,1 op oppervlak |

- **Typografie:** Righteous voor koppen en cijfers, Work Sans 500 tot 700 voor tekst.
- **Vormtaal:** pillen en cirkels, 2 px warme randen, drie strepen (terracotta, oker, petrol) als terugkerend motief: gebogen band bovenaan, gestreepte XP-balk, ticket met uitsparingen. Iconen 2,2 px.
- **Beweging:** zacht en zonnig: de zonnestralen draaien langzaam, de gestreepte tijdbalk krimpt, juist poppt licht op.
- **Kernmomenten:** juist = groene pil met vinkje en "Juist"; fout = zachtrode pil met kruis en "Jouw keuze"; kaartje "Naast!" met uitleg. Reeks = oranje medaille met getal. Beloning = petrol cirkel "+86 XP" in een zon, gestikte badge. Klassement = pil-rijen met goud/zilver/brons-schijfjes, jouw rij petrol.
- **Risico's:** nostalgie die jongere spelers niet herkennen; veel warme tinten dicht bij elkaar (terracotta ligt dicht bij fout-rood, dus terracotta nooit voor foutvlakken); Righteous heeft weinig gewichten, wat de hiërarchie beperkt.
- **Migratie:** middel. Licht als basis (zoals optie 1), plus het strepenmotief als paar herbruikbare klassen; componenten zelf veranderen weinig.

---

## Migratie, voor alle opties

- **Tokens:** de rolnamen in `app.css` (`--card`, `--text`, `--muted`, `--accent`, `--ok`, `--fail`, `--radius`, `--tap`) blijven bruikbaar. De "op donker"-varianten (`--accent-text`, `--ok-text`, `--fail-text`, `--sb-digit`, `--on-field-muted`) moeten per optie herdacht worden. Nodig in elke optie: `--font-head`, `--font-body` en `--font-num` (vervangt `--digits`).
- **Lichte opties (1, 2, 3, 6):** `color-scheme` wordt `light`, `.surface-light` wordt grotendeels de standaard, en het levende veld (`body::before`, `PitchFx.razor`, `pitch.js`, `html.pitch-night`) verdwijnt of krijgt een nieuwe vorm. Dat laatste is het grootste stuk werk en raakt `animatie-bouwer`.
- **Fonts:** voor een PWA beter zelf hosten (woff2 in `wwwroot`) dan via Google Fonts, zodat de app offline en zonder layoutsprong laadt. Reken op 2 à 3 families met 2 à 4 gewichten.
- **Geen impact** op spelregels, API's of puntentelling: alles is presentatie.

## Top 3 en aanbeveling

1. **Speelplein (1), mijn aanbeveling.** Truitje Trek is een dagelijkse gewoonte met reeksen, XP, levels en badges, en Speelplein geeft elk van die momenten een duidelijke, belonende vorm (principe 4: spelbeleving), terwijl fout mild blijft ("De volgende zit erin" met een neutrale knop). Het is licht, dus goed leesbaar buiten en in de zon, en het verschil met het huidige scorebord is meteen voelbaar. De dikke knoppen zijn per definitie goede duimdoelen (principe 1). De migratie is middel en grotendeels token-werk plus het veld. Tem het kinderlijke risico met spaarzaam gebruik van de mascotte en een volwassen toon in de copy.
2. **Perskamer (2).** De veiligste, meest tijdloze en goedkoopste keuze, ideaal als de eigenaar vooral "volwassen en premium" wil. Combineer eventueel met de veerkrachtige beloningsmomenten van Speelplein om de koelte te breken.
3. **Nachtmatch (4).** De sterkste donkere optie: een echte sfeersprong (koel en elektrisch in plaats van groen veld) en de kleinste technische stap, omdat de structuur al donker is. Interessant als de eigenaar donker wil houden of een avondmodus overweegt.

Driekleur (3) is het meest eigenzinnig en Belgisch, maar het rood/fout-conflict en de migratie maken het riskant als hoofdstijl. Het kan wel een sterke vorm zijn voor een Rode Duivels-special. Ereloge (5) en Mundial '86 (6) zijn mooie sferen die beter passen als thema voor de Gouden Bal of een retro-special dan als dagelijkse huisstijl.

**Open vragen voor de eigenaar:** wil je licht of donker als basis (of later beide)? Mag de app een mascotte krijgen? Moeten specials hun eigen thema kunnen krijgen (dat zou opties 3, 5 en 6 een tweede leven geven)?
