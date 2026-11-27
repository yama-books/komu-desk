# KOMU DESK v0.1.4 - Bento menu and shared desktop OAuth

## Asset inventory
Original: generated monochrome 4x4 sheet approved for slicing. Cropped each glyph separately, removed white background, centered within 64×64 transparent PNG (charcoal RGB 40,40,40, 4 alpha levels), embedded as WPF Resource. No runtime network or outside asset library. The original 1254×1254 sheet is retained in the source conversation; only cut assets are stored here.

| No. | ID | Design / usage |
|---|---|---|
| 01 | calendar | Local events |
| 02 | timetable | Weekly timetable, versions |
| 03 | bell | Bell schedule |
| 04 | classes | Grade, class, teaching group |
| 05 | lesson-count | Lesson counts |
| 06 | notebook | Subjects, day changes |
| 07 | tasks | Local tasks |
| 08 | printer | Reports |
| 09 | spreadsheet | Excel import |
| 10 | appearance | Year / display |
| 11 | backup | Backup |
| 12 | update | App update / sync |
| 13 | settings | Header settings |
| 14 | restore | Restore |
| 15 | connection | Google Calendar |
| 16 | cat | Widget / menu mascot |

All files: src/KomuDesk.Desktop/Assets/Icons/<id>.png. WPF caches decoded images. Settings menu preserves 18 original actions, grouped into 3 sections of 6 tiles, three columns, scrollable. Same rounded template for shared buttons; state-driven hover/press transition, small 98% scale and 1 DIP shift, honors OS ClientAreaAnimation at startup. Existing small native buttons and controls outside the shared template may remain; no claim of complete visual-system migration. Cat is static; expression/ICO work remains separate.

## Shared Google Calendar OAuth
Sources:
- https://github.com/taekit93/deskcal (direct installed-app OAuth/PKCE and local token storage).
- https://developers.google.com/identity/protocols/oauth2/native-app (system browser, loopback, PKCE)
- https://developers.google.com/workspace/calendar/api/auth (minimal read-only scopes)
- https://developers.google.com/identity/protocols/oauth2/production-readiness/overview (audience, testing, production)
- https://developers.google.com/identity/protocols/oauth2 (external Testing refresh tokens generally expire after seven days for Calendar)
- https://docs.cloud.google.com/dotnet/docs/reference/Google.Apis/latest/Google.Apis.Auth.OAuth2.GoogleWebAuthorizationBroker (official .NET option)
No third-party code copied. Preserve existing direct PKCE browser flow, state, scopes, token handling and DPAPI. A shared organizer-managed Desktop OAuth client identifies the app; each participant independently consents to their own Calendar. No shared tokens/account, no remote backend, no Tasks or school data uploads. One client per app/environment, not one Google Cloud project per user. Use Internal if all participants are in the same eligible Workspace organization and administrator approves; otherwise External Testing for named testers (up to 100; generally 7-day token expiry), then assess In production and applicable verification before wider use. Personal mode cannot serve multiple people.

## Provisioning and consent
Organizer creates one Google Cloud project, enables Calendar API, configures consent/audience, chooses Desktop app client, and registers testers or obtains organizational approval. No actual OAuth credentials have been created or supplied in this task. Normal button resolves local DPAPI client config, existing session credential, or optional developer-provisioned assembly resource (in that order). If absent, explains setup is missing; no fake login and no network. Admin can import the organizer's JSON once under advanced settings; future reauth does not require file selection. Existing session and snapshot retained until successful login. Client config persists separately in oauth-client.bin, no token in that file. Normal release without configured resource remains unconfigured for a new user. Optional trusted organizer build: dotnet publish ... -p:KomuDeskOAuthConfig=<absolute path to Desktop OAuth JSON outside repository>. An installed-app client ID/secret is not a confidential server secret and embedding it makes it extractable: do not publish an embedded client in public artifacts without explicit owner approval and Google policy/quota review. Never commit real JSON, user refresh/access tokens or account credentials. Private organizer package or one-time managed local provisioning are supported; public release today has no actual OAuth client embedded. Existing app data and client config persist through ordinary public updates. For truly one-click first login on a new machine a provisioned build or preprovisioned config is required.
Validation: core tests, Windows widget/menu smoke, real account consent and sync not yet verified. Google Workspace policy may still block access.
