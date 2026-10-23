# Beveiligingsreview "Aanmelden met Google" – 23 oktober 2026

Review door de security-wachter van commit `73e649a` ("Aanmelden met Google: koppelen aan bestaand account na wachtwoord, nieuwe speler kiest gebruikersnaam"), eerste stap van week 44 in [de planning](../backlog/planning-2026-10-23.md). Niets aangepast. Testsuite op dezelfde commit: 349 geslaagd, 0 gefaald, 1 overgeslagen (`PostgresMigrationTests`, Podman).

## Oordeel

**Nog niet naar productie.** Eerst bevinding 1 oplossen (een paar regels plus een test). Bevindingen 2 tot 7 mogen na de deploy. Geen kritieke of hoge bevindingen.

## Bevindingen

### 1. Midden: automatisch koppelen op e-mailadres laat een tweede Google-account binnen

- **Waar:** `src/VoetbalQuiz.Server/Services/GoogleLoginService.cs:40-43`.
- **Wat:** als er een account met het Google-adres bestaat en `EmailConfirmed` true is, wordt de Google-identiteit zonder wachtwoord gekoppeld. `EmailConfirmed` is in deze code alleen true voor accounts die al via Google aangemaakt (r. 94) of gekoppeld (r. 61) zijn. De tak voegt dus een **tweede** Google-identiteit (andere `sub`) toe aan een account dat al een Google-login heeft, enkel omdat het e-mailadres gelijk is.
- **Misbruik:** speler of admin X koppelt Google met een adres buiten Gmail (hotmail, werkadres). Later krijgt iemand anders dat adres (heruitgegeven mailbox, collega erft het werkadres) en maakt er een Google-account op. Google zet `email_verified=true` en de nieuwe eigenaar komt zonder wachtwoord binnen op het account van X. Admin-rechten hangen aan de gebruikersnaam (`CurrentUser.IsAdminName`), dus bij een admin ook het admin-paneel. Google raadt zelf aan `sub` als sleutel te gebruiken en waarschuwt dat `email_verified` buiten Gmail verouderd kan zijn.
- **Strijdig met de README**, die zegt dat een bestaand account niet automatisch op e-mailadres gekoppeld wordt.
- **Oplossing:** r. 40-43 vervangen door: bestaat er een account met dit adres, geef dan altijd `GoogleNext.SignInWithPasswordFirst`. Heeft dat account al een Google-login (`GetLoginsAsync` bevat `Provider`) of geen wachtwoord, weiger dan met een aparte code ("Er hoort al een ander Google-account bij dit adres, contacteer de beheerder").
- **Tests:** `Existing_account_with_a_confirmed_email_links_automatically` omkeren; toevoegen: "tweede Google-sub met hetzelfde bevestigde adres koppelt niet".

### 2. Laag: Google-account blokkeren via mislukte wachtwoordpogingen

- **Waar:** `src/VoetbalQuiz.Server/Endpoints/AccountEndpoints.cs:45` en `:100`.
- **Wat:** een account zonder wachtwoord kan niet met een leeg wachtwoord inloggen (goed), maar elke poging telt voor de lockout, en `/google/klaar` weigert daarna ook de Google-aanmelding.
- **Misbruik:** vijf foute pogingen op de naam van een rivaal (namen staan in het klassement) net vóór de daily, en die kan zich 5 minuten niet aanmelden. Herhaalbaar, want er is geen rate limiting.
- **Oplossing:** de lockout-check op r. 100 laten vallen; Google bewijst de identiteit al. Een rate limiter op `/login` blijft aan te raden.

### 3. Laag: mislukte koppeling na het wachtwoord gebeurt stil

- **Waar:** `AccountEndpoints.cs:52-56`. Het pad zelf is veilig (`LinkAfterPasswordAsync` eist provider Google, `email_verified` en exact hetzelfde adres), maar het resultaat wordt genegeerd. Een speler met een typfout in zijn adres denkt dat Google gekoppeld is en krijgt de volgende keer weer "koppel".
- **Oplossing:** resultaat in de response meegeven of loggen zonder e-mailadres.

### 4. Laag: "Wachtwoord instellen" zonder herbevestiging

- **Waar:** `AccountEndpoints.cs:158-160`. `HasPasswordAsync` wordt op de server bepaald, dus een account met wachtwoord gaat altijd langs `ChangePasswordAsync`. Restrisico: wie een sessie van een Google-only speler in handen krijgt (onbewaakte telefoon), zet een wachtwoord en houdt blijvend toegang.
- **Oplossing (optioneel):** `AddPasswordAsync` enkel kort na een Google-aanmelding toestaan; daarna `UpdateSecurityStampAsync` vóór `RefreshSignInAsync` zodat andere sessies vervallen.

### 5. Laag: cookies en forwarded headers

- **Waar:** `src/VoetbalQuiz.Server/Program.cs:57-64` en `:66-71`.
- SameSite Lax in plaats van Strict is verantwoord: alle `MapGet`-endpoints zijn lezend, en azurewebsites.net staat op de Public Suffix List.
- `SecurePolicy` staat op SameAsRequest, dus de Secure-vlag hangt af van `X-Forwarded-Proto`. Zet `CookieSecurePolicy.Always`.
- `KnownIPNetworks.Clear()` met `ForwardLimit=1` volgt de richtlijn voor App Service; `RemoteIpAddress` wordt nergens gebruikt. Bouw er geen IP-rate-limiting op zonder dit te herzien.
- Er staat geen `UseHsts`; controleer dat "HTTPS Only" aan staat op Azure.

### 6. Laag: dubbel e-mailadres bij gelijktijdige aanvragen

- **Waar:** `GoogleLoginService.cs:98`. `RequireUniqueEmail` is enkel een applicatiecontrole; de Identity-index op `NormalizedEmail` is niet uniek. Twee gelijktijdige POST's op `/google/nieuw` of `/register` met hetzelfde adres geven twee accounts, waarna `FindByEmailAsync` een exception gooit.
- **Oplossing:** migratie met een unieke index op `NormalizedEmail`.

### 7. Laag: localhost-redirect-URI in de productieclient

- **Waar:** README, sectie "Aanmelden met Google", stap 1: `http://localhost:5174/signin-google` zit in dezelfde OAuth-client als productie.
- **Oplossing:** aparte OAuth-client voor ontwikkeling, zodat het productiegeheim niet op elke ontwikkelmachine staat.

## Wat goed zit

- OAuth-flow: state en correlation cookie via de Google-handler, PKCE standaard aan, `sub` als ProviderKey, geen id_token dus geen nonce nodig. `OnRemoteFailure` stuurt naar een vaste interne URL en logt geen tokens.
- Geen open redirect: `ReturnPath.Safe` weigert `//`, `:`, `\`, controletekens en niet-ASCII; de server gebruikt `LocalRedirect`; de tests dekken de bekende trucs.
- Nieuwe speler, stap 1 naar 2: de identiteit zit in de versleutelde cookie `Identity.External` (data protection met sleutelring in de database, 5 minuten geldig), wordt na gebruik afgemeld en is niet te vervalsen. Een account aanmaken zonder Google kan niet.
- Gebruikersnaamlengte wordt op de server gecontroleerd. Zonder ClientId of ClientSecret verdwijnen knop en endpoints (`/google` geeft 404). Geen secrets in git, e-mailadressen worden niet gelogd, `dotnet list package --vulnerable --include-transitive` meldt niets.

## Ontbrekende tests (voor test-bouwer, endpointtests via WebApplicationFactory)

- Tweede Google-sub met hetzelfde bevestigde adres koppelt niet (bevinding 1).
- `/login` met een externe cookie van een ander adres koppelt niet.
- `/password` op een account met wachtwoord zonder `CurrentPassword` geeft 400.
- `/google/nieuw` zonder externe cookie geeft 404 of 400.
- `/google/klaar` met `terug=//evil` blijft op de eigen host.
- De echte claim-mapping: de test zet `"True"` zelf, terwijl de echte waarde via `MapJsonKey` uit een JSON-boolean komt. Een test die `JsonKeyClaimAction` uitvoert op `{"email_verified":true}` bewijst dat `IsEmailVerified` niet stil op false valt (dat faalt veilig, maar dan werkt Google voor niemand).

## Niet gecontroleerd

- Azure: HTTPS Only, app-instellingen, aantal instanties.
- Google Cloud Console: geregistreerde redirect-URI's en of het toestemmingsscherm nog in testmodus staat.

## Buiten deze commit

- `appsettings.Development.json` staat in git met het wachtwoord van de lokale Postgres (enkel localhost, laag); hoort beter in user-secrets.
- Nergens rate limiting op aanmelden of registreren.

## Bronnen

- Google, Authenticate with a backend server: https://developers.google.com/identity/sign-in/web/backend-auth
- Google, Verify the Google ID token: https://developers.google.com/identity/gsi/web/guides/verify-google-id-token
