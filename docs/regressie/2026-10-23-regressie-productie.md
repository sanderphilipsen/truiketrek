# Regressie vóór productie – Truitje Trek (23 oktober 2026)

Getest op commit `80d24ed` (main), als gepubliceerde Release-versie op http://localhost:5177 met een eigen database (`voetbalquiz_preview`), op gsm-formaat (375 px, steekproef op 320 px).
Drie testers speelden elk een deel door in eigen browsertabs met eigen testaccounts; de security-wachter reviewde de Google-login apart. Live stond op dat moment `cf724ce` (22/10).

Nieuw sinds de vorige deploy: aanmelden met Google (`73e649a`), de teller "nog te spelen" en de knop "Spelen" in de Academie (`cbf317d`), admin ziet leden en "laatst online" met migratie `UserLastSeen` (`6fb0dc2`), terugknop na de daily (`e529a23`), strook "Bug of idee melden" op Home (`ee8ff51`), tegels 85 % opaak (`d5cea68`).

## Conclusie

**Klaar voor productie.** Geen enkele tester of de beveiligingsreview vond een blokkerend probleem. De kernlus (aanmelden, daily, academie, klassement, beloningen, profiel, online, derby, specials, admin, feedback) werkt. Er is een reeks kleine punten voor na de release; de meeste bestonden al vóór deze versie.

| Deel | Oordeel |
|---|---|
| Build, tests, migraties, publish | ✅ groen |
| Beveiliging Google-login | ✅ geen hoge of kritieke bevinding, 6 lage |
| Spelerflows, daily, academie, profiel, klassement | ✅ klaar |
| Online partijtjes (5 wedstrijden, 11 types, derby) | ✅ klaar |
| Admin, specials, feedback, rechten, PWA | ✅ klaar |

## Automatische controles

- `dotnet test -c Release`: **349 geslaagd, 0 gefaald, 1 overgeslagen** (`PostgresMigrationTests`, Testcontainers vindt Podman niet; zoals op 22/10).
- `dotnet ef migrations has-pending-model-changes`: geen openstaande modelwijzigingen. Nieuwe migratie sinds de vorige deploy: `20261023061602_UserLastSeen` (enkel `AddColumn LastSeenAt`, nullable; `DropColumn` staat alleen in `Down`). Geen dataverlies.
- `dotnet publish -c Release` (launch-configuratie `voetbalquiz-published`): slaagt; `GET /api/status` geeft `80d24ed…` en `updateAt: null`; geen serverfouten in de logs bij opstart, migratie en seed.
- `GET /api/account/options` geeft `{"google":true}` (sleutels in user-secrets); `/api/account/google` stuurt door naar accounts.google.com met PKCE; `/api/account/google/nieuw` zonder Google-sessie geeft 404.
- `dotnet list package --vulnerable --include-transitive`: niets voor Server en Client.

## Beveiliging (Google-login, commit `73e649a`)

Geen open redirect (`ReturnPath.Safe` plus `LocalRedirect`), geen accountovername via koppelen (e-mail vergeleken met de aangemelde gebruiker, enkel bevestigde adressen, pas na een geslaagd wachtwoord), geen bruikbare CSRF-paden met SameSite Lax, wachtwoord instellen zonder huidig wachtwoord wordt serverkant afgedwongen, geen secrets in git. Zes lage bevindingen, twee om kort na de deploy mee te nemen:

1. **Lockout van Google-spelers** (`AccountEndpoints.cs` /login): foute wachtwoordpogingen op een account zonder wachtwoord tellen mee voor de lockout, en `/google/klaar` weigert dan. Fix: zonder wachtwoord meteen dezelfde foutmelding geven, en de lockoutcontrole op het Google-pad schrappen.
2. **Cookie `Secure` expliciet** (`Program.cs`): `SecurePolicy` staat op `SameAsRequest` en hangt dus van het doorgestuurde schema af. Fix: `CookieSecurePolicy.Always` en `UseHsts()` buiten Development.
3. `KnownProxies`/`KnownIPNetworks` leeg: nu zonder gevolg (ForwardLimit 1, IP nergens gebruikt), maar wel vóór er rate limiting per IP komt de private reeksen toevoegen i.p.v. wissen.
4. Een bezet (onbevestigd) e-mailadres blokkeert Google voor de echte eigenaar; er is enkel een wachtwoordreset door de admin als uitweg. Idee: admin-actie "e-mailadres vrijgeven" op accounts zonder bevestigd adres.
5. Automatisch koppelen op bevestigd e-mailadres koppelt in de praktijk een tweede Google-identiteit; beter niet koppelen als er al een Google-login is.
6. Race in `POST /google/nieuw` kan een weesaccount achterlaten (geen unieke index op `NormalizedEmail`); `try/catch` rond `AddLoginAsync` of een unieke index.

Niet gecontroleerd: de Azure-app-instellingen (Google-sleutels, `ASPNETCORE_FORWARDEDHEADERS_ENABLED`), of de live cookie nu `Secure` heeft, de instellingen in de Google Cloud Console. Tip: een aparte OAuth-client voor lokaal, zodat het productiesecret niet in user-secrets van de ontwikkelaar staat.

## 🟡 Klein: kan na de release

**Nieuw in deze versie**
- Academie: een niet-afgewerkte poging telt als "gespeeld" (grijs pijltje, teller daalt), terwijl de dagpagina "niet afgewerkt" zegt. Misleidend; beslissen of een afgebroken poging moet tellen.
- Academie, resultaatpagina (`/academie/poging/{id}`) toont nog het vraagtype, terwijl lijst en dagpagina dat verbergen.
- Academie, dagpagina: "1 speler speelden die dag" moet "speelde" zijn.
- `/login` toont het formulier ook voor een al aangemelde speler.

**Online** (bestond al)
- `/online/99999` toont "Deze wedstrijd bestaat niet." én een eeuwige laad-indicator eronder (`Wedstrijd.razor:30-38`: foutmelding en `_state is null`-spinner tegelijk zichtbaar).
- Na een walk-over (tegenstander vertrekt) komt bij geen van beide spelers een beloningspopup; de XP komt gebundeld in de popup van de volgende wedstrijd (zie ook de "ontbrekende popup" van 22/10: zelfde claimmechanisme).
- Derby, Propere handen: de rij van de dief toont "+90" terwijl de tussenstand +150 stijgt (eigen 90 + buit 60).
- "Wachten op de aftrap door regb-jos" breekt op het koppelteken; Home-chips "nog geen plaats"/"start je reeks" zijn 32 px hoog.

**Spelerflows** (bestond al)
- Placeholder "Wie ben ik?" staat ook bij "Welke club is dit?" (emoji-raadsel).
- `/vraag/99` toont "Vraag 99 / 10" boven "Deze vraag bestaat niet."
- Klassement Maand: lange namen breken midden in het woord; tabknoppen 37 px hoog.
- Home op 320 px en `/special/{id}`: "ANDERLECHT" breekt in de special-banner ("ANDERLEC / HT").
- Elke anonieme paginalading geeft twee 401's op `/api/account/me` in de console (functioneel onschuldig), plus de waarschuwing `<link rel=preload> invalid href` uit `index.html:12`.

**Admin** (bestond al, ook in `cf724ce`)
- Knop "Opslaan en goedkeuren" op `/admin/spelers/{id}` en `/admin/vragen/{id}`: witte tekst op lichtgroen, onleesbaar. `.surface-light .btn:not(.primary)` (`app.css:290`) wint van `.btn.approve` (`app.css:1333`). Eén regel fix: specificiteit verhogen of `.approve` uitsluiten.
- Horizontale scroll op 375 px in de speler- en specialeditor (`.row-2` met inputs die niet krimpen; `minmax(0,1fr)` en `min-width:0` nodig).
- "+ Speler toevoegen" e.d. op de donkere achtergrond met donkergele tekst (contrast ±2,2:1; `--sb-digit` overschreven in de lichte scope).
- Tikdoelen onder 44 px: admin-tabs 37, filterchips 32, mix-chips 34, selects 40.
- Planning: na de weigering "minstens één vraagtype" blijft de laatste schakelaar visueel uit tot een herlaad.
- Zoekbalk spelers filtert pas na Enter; lange ledennamen breken midden in het woord.
- Eén consolefout `<circle> attribute r: -0.01` (SVG-animatie, bron niet gevonden).

## Dekking

- **Spelerflows:** registreren (dubbele naam, kort wachtwoord), afmelden, aanmelden met naam en e-mail, fout wachtwoord, wachtwoord vergeten; Google-knop (gaat naar accounts.google.com, niet afgerond), foutteksten `fout=google|koppel|onbevestigd|geblokkeerd` en onbekende code, `/login/naam` zonder Google-sessie ("verlopen" met terugknop), terugpad `online/12` en de onveilige varianten `//evil.com` en `https://evil.com` (blijven op de app). Daily 10/10 (enkel emoji-raadsel die dag), herladen en browser-terug midden in een vraag (klok loopt door), dagoverzicht, beloningspopup (+226 XP, level 2, badge "Aftrap"), profiel (avatar, wachtwoord wijzigen met huidig wachtwoord, statistieken), academie (teller 3→2→1, gele stip en "Spelen", 22 okt volledig en 19 okt deels), klassement (Vandaag, Maand met bladeren, Online). 320 px zonder horizontale scroll op Home, Login, Registreren, Academie.
- **Online:** 5 wedstrijden en 2 annuleringen met drie accounts op aparte origins; 11 vraagtypes (alle behalve Rugnummer en Mannetje); lobby (aanmaken, Doe mee, link, anonieme link via `?terug=`, opheffen met bevestiging, verlaten, volzet, toeschouwer, onbestaande en afgelopen wedstrijd, HTML-escaping van namen); gelijkspel 0–0 ("Gelijkspel!", +25 XP, geen zege); zeges (+100 XP, badges "Online debuut" en "Eerste zege"); derby met Rode kaart en Propere handen, wapenstilstand, derby-rapport; vertrek halverwege ("telt niet als zege"); 4× herladen midden in een vraag zonder dubbele punten; Elo nagerekend (1031/1027, 969/960).
- **Admin:** als admin `sander` (Development): dashboard, leden met "laatst online", spelers zoeken/openen/bewaren/toevoegen/afwijzen, alle 10 vraageditors, planning (mix, type uit/aan, weigering), special uit sjabloon aanmaken/bewerken/spelen (8 vragen)/vergrendeling/klassement/top 3 op Home, feedback heen en terug; 403 op alle admin-API's voor een gewone speler, 401 zonder sessie, AdminGuard; manifest, service worker en iconen laden.
- **Niet getest:** echte Google-aanmelding en de koppel- en gebruikersnaamstappen daarna (kan enkel met een echt Google-account), een afgelopen special in de academie, Rugnummer en Mannetje online, derby-wapens Bengaals vuur, Wissel, Trump, Gokchinees en Verlenging, special verwijderen, admin-wachtwoordreset, donkere modus, de service worker bij een update, de top 3 van een special met drie of meer spelers.

Testdata in `voetbalquiz_preview`: accounts `rega-eva`, `regb-jos`, `regb-mia`, `regb-kim`, `regcA`, `sander`; special 1 "Regc Test: De Topper"; afgewezen speler "Regc Testspeler"; feedback "Regc testmelding". Tip voor een volgende ronde: geef elke tester een eigen origin (`x.localhost:5177`), anders overschrijven ze elkaars sessiecookie.
