# Probabilites Loto & EuroMillions

Application web vitrine en francais, informative et statistique, construite avec .NET 10.

## Objectifs
- Fournir un socle pro/auditable: API, Web Blazor, Worker, architecture en couches.
- Exposer des indicateurs de statut (placeholders) sur les tirages Loto et EuroMillions.
- Preparer le terrain pour la synchronisation planifiee des tirages et les emails d'abonnement.
- Proposer un demarrage local via Docker Compose et via .NET Aspire.

## Avertissements importants
- Ce projet est strictement informatif/statistique.
- Aucun algorithme de prediction de tirage n'est fourni.
- Jeu responsable: les jeux d'argent comportent des risques (dependance, isolement, endettement).
- Non affilie a FDJ.

## RGPD (etat actuel du socle)
- Les emails d'abonnement sont prevus uniquement pour le service de notification.
- Un mecanisme de desinscription par lien unique est prevu dans le modele de donnees.
- Aucune reutilisation marketing des emails n'est incluse dans ce socle.

## Stack technique
- .NET 10
- ASP.NET Core Web API (`src/Api`)
- Blazor Web App + MudBlazor (`src/Web`)
- Worker + Quartz.NET (`src/Worker`)
- EF Core + Npgsql (`src/Infrastructure`)
- OpenTelemetry + Serilog (logs/traces/metrics)
- .NET Aspire (`src/AppHost`, `src/ServiceDefaults`)
- PostgreSQL + MailHog (dev)

## Structure
```text
/
  src/
    AppHost/
    ServiceDefaults/
    Api/
    Worker/
    Web/
    Domain/
    Application/
    Infrastructure/
  tests/
    UnitTests/
    IntegrationTests/
  ops/
    docker-compose.dev.yml
    .env.example
  .github/workflows/ci.yml
  README.md
  LICENSE
  global.json
```

## Prerequis
- .NET SDK 10.0.103+
- Docker Desktop (ou Docker Engine + Compose)

## Lancer avec Aspire (dev local recommande)
1. Definir un mot de passe Postgres pour `AppHost`:
```powershell
dotnet user-secrets set "Parameters:postgres-password" "change-me-local-only" --project src/AppHost
```
2. Lancer l'orchestrateur:
```powershell
dotnet run --project src/AppHost
```
3. Ouvrir le dashboard Aspire puis acceder aux services:
- Web: URL exposee par Aspire
- API: URL exposee par Aspire (`/swagger`, `/api/status`, `/health`)

## Lancer avec Docker Compose
1. Optionnel: creer `ops/.env` a partir de `ops/.env.example`.
2. Depuis `ops/`, lancer:
```powershell
docker compose -f docker-compose.dev.yml up --build
```
3. Endpoints:
- Web: `http://localhost:8080`
- API Swagger: `http://localhost:8081/swagger`
- API status: `http://localhost:8081/api/status`
- API health: `http://localhost:8081/health`
- MailHog: `http://localhost:8025`
- PostgreSQL: `localhost:5432`

## Configuration (variables d'environnement)
- `ConnectionStrings__Postgres`
- `Api__BaseUrl`
- `Smtp__Host`
- `Smtp__Port`
- `Smtp__UseStartTls`
- `Smtp__Username`
- `Smtp__Password`
- `Jobs__SyncDraws__IntervalMinutes`
- `Jobs__SendSubscriptions__IntervalMinutes`
- `Jobs__SendSubscriptions__DryRun`
- `Jobs__SendSubscriptions__TestRecipient`

## Qualite et CI
- Nullable active partout
- Analyzers .NET actifs
- Warnings as errors sur `src/*`
- Health checks exposes sur API/Web (`/health`)
- Pipeline GitHub Actions: restore, build, tests, verification format

## Commandes utiles
```powershell
dotnet restore
dotnet build
dotnet test
```
