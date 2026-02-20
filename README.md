# Probabilités Loto & EuroMillions

Application web informative et statistique autour des tirages Loto et EuroMillions, construite en .NET 10 avec une architecture `web + api + worker + postgres`.

## Objectif du projet
- fournir un socle auditable et exploitable en production;
- synchroniser les archives FDJ dans PostgreSQL;
- proposer des statistiques et une génération de grilles explicable;
- exposer une administration minimale, protégée;
- garantir une observabilité complète (logs, traces, métriques, santé).

## Fonctionnalités principales
### Observabilité
- logs Serilog structurés (JSON) sur `Api`, `Worker`, `Web`;
- OpenTelemetry traces + métriques (instrumentation HTTP/runtime + métriques métier DrawSync);
- health checks:
  - `postgres` (toujours actif côté API),
  - `smtp` (optionnel via `HealthChecks:Smtp:Enabled`).

### Administration minimale
- endpoint protégé `POST /api/admin/sync` (header `X-Api-Key`);
- endpoint protégé `GET /api/admin/sync-runs`;
- page Web `/admin` protégée par authentification HTTP Basic:
  - visualisation des derniers `SyncRuns`,
  - bouton `Sync maintenant`.

### Robustesse d'ingestion
- parsing HTML tolérant aux variations mineures (labels/URLs);
- parsing CSV/Excel tolérant aux variations de colonnes (aliases + fallback par tokens);
- cache HTTP conditionnel sur les pages d'historique FDJ:
  - `ETag` / `If-None-Match`,
  - `Last-Modified` / `If-Modified-Since`,
  - persistance du cache dans `sync_state`.

### Qualité et tests
- tests unitaires sur les parties critiques:
  - règles de combinaisons,
  - sampling pondéré,
  - parsing des fichiers FDJ;
- tests d'intégration `API + PostgreSQL` via Testcontainers.

## Architecture
```text
src/
  AppHost/           Orchestration .NET Aspire
  ServiceDefaults/   Observabilité commune, discovery, résilience
  Api/               API minimale ASP.NET Core
  Worker/            Jobs Quartz (sync + envois)
  Web/               Interface Blazor Server + MudBlazor
  Domain/            Modèle métier
  Application/       Contrats et DTO
  Infrastructure/    EF Core, services, ingestion, SMTP

tests/
  UnitTests/
  IntegrationTests/

ops/
  docker-compose.dev.yml
  docker-compose.prod.yml
  .env.example
```

## Prérequis
- .NET SDK 10.0.103+;
- Docker Desktop (ou Docker Engine + Compose).

## Démarrage rapide (Docker Compose, recommandé)
Depuis la racine du dépôt:

```powershell
docker compose up --build
```

Services disponibles:
- Web: `http://localhost:8080`
- API Swagger: `http://localhost:8081/swagger`
- API Health: `http://localhost:8081/health`
- MailHog: `http://localhost:8025`
- PostgreSQL: `localhost:5432`

Identifiants admin Web par défaut (dev):
- utilisateur: `admin`
- mot de passe: `dev-admin-ui-password`

## Démarrage avec Aspire
1. Définir le mot de passe PostgreSQL d'AppHost:
```powershell
dotnet user-secrets set "Parameters:postgres-password" "change-me-local-only" --project src/AppHost
```
2. Lancer:
```powershell
dotnet run --project src/AppHost
```
3. Ouvrir le tableau de bord Aspire puis naviguer vers `web`, `api`, `worker`.

## Variables de configuration importantes
- `ConnectionStrings__Postgres`
- `Admin__ApiKey` / `ADMIN_API_KEY`
- `Admin__WebUsername` / `ADMIN_WEB_USERNAME`
- `Admin__WebPassword` / `ADMIN_WEB_PASSWORD`
- `Api__BaseUrl`
- `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASS`, `SMTP_FROM`
- `PUBLIC_BASE_URL`
- `SUBSCRIPTIONS_TOKEN_SECRET`
- `HealthChecks__Smtp__Enabled`
- `Jobs__SyncDraws__*`, `Jobs__SendSubscriptions__*`
- `DrawSync__Loto__*`, `DrawSync__EuroMillions__*`

## Commandes utiles
```powershell
dotnet restore
dotnet build
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

## Endpoints API utiles
- `GET /api/status`
- `GET /api/stats/{game}`
- `POST /api/grids/generate`
- `POST /api/subscriptions`
- `GET /api/subscriptions/confirm`
- `GET /api/subscriptions/unsubscribe`
- `GET /api/subscriptions/status`
- `DELETE /api/subscriptions/data`
- `POST /api/admin/sync`
- `GET /api/admin/sync-runs`

## Documentation complémentaire
- ADR: `docs/adr/0001-observabilite-admin-ingestion.md`
- Schéma base: `docs/schema-db.md`
- Déploiement Docker prod: `docs/deploiement-docker.md`

## Avertissements
- projet strictement informatif/statistique;
- aucune prédiction de tirage;
- jeu responsable: les jeux d'argent comportent des risques (dépendance, isolement, endettement);
- projet non affilié à FDJ.
