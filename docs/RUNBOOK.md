# RUNBOOK Exploitation
Ce document regroupe les détails d’exploitation (home/self-hosted) volontairement exclus du README vitrine.

## Déploiement manuel via GitHub Actions
Workflow: [`.github/workflows/deploy-manual.yml`](../.github/workflows/deploy-manual.yml)

Paramètres `workflow_dispatch`:
- `environment` (défaut: `home`)
- `ref` (défaut: `main`)

Runner cible:
- labels: `[self-hosted, linux, ci]`

Secrets requis côté GitHub:
- `SSH_HOST`
- `SSH_USER`
- `SSH_PRIVATE_KEY`
- `SSH_PORT` (optionnel, défaut `22`)

Script appelé par le workflow:
- [`scripts/deploy-home.sh`](../scripts/deploy-home.sh)

## Cible `home` (comportement scripté actuel)
Le script de déploiement utilise:
- dossier applicatif: `/home/arnaud/apps/proba-loto-euromillions`
- compose: `deploy/home.compose.yml`
- env file: `deploy/home.env`
- projet compose: `probaloto-home`
- healthcheck post-déploiement: `http://127.0.0.1:8083/health`
- timeout healthcheck: `300` secondes, polling toutes les `5` secondes

Si `deploy/home.env` est absent:
- le script le crée depuis `deploy/home.env.example`
- puis applique `chmod 600`

## Démarrage manuel home (hors workflow)
```bash
docker compose -p probaloto-home -f deploy/home.compose.yml --env-file deploy/home.env up -d --build --remove-orphans
```

## Variables minimales `deploy/home.env`
Source: [`deploy/home.env.example`](../deploy/home.env.example)

- `POSTGRES_USER=<DB_USER>`
- `POSTGRES_PASSWORD=<DB_PASSWORD>`
- `CONNECTIONSTRINGS__POSTGRES=Host=postgres;Port=5432;Database=probabilites_loto;Username=<DB_USER>;Password=<DB_PASSWORD>`
- `ADMIN_API_KEY=<ADMIN_API_KEY>`
- `ADMIN_WEB_USERNAME=<ADMIN_USER>`
- `ADMIN_WEB_PASSWORD=<ADMIN_PASSWORD>`
- `PUBLIC_BASE_URL=https://demo.example.com`
- `SUBSCRIPTIONS_TOKEN_SECRET=<LONG_RANDOM_SECRET>`
- `HEALTHCHECKS_SMTP_ENABLED=true`
- `MAIL__ENABLED=true`
- `MAIL__FROM=no-reply@example.com`
- `MAIL__FROMNAME=Proba Loto`
- `MAIL__BASEURL=https://demo.example.com`
- `MAIL__SMTP__HOST=smtp.example.com`
- `MAIL__SMTP__PORT=587`
- `MAIL__SMTP__USESSL=true`
- `MAIL__SMTP__USERNAME=<SMTP_USERNAME>`
- `MAIL__SMTP__PASSWORD=<SMTP_PASSWORD>`
- `MAIL__SCHEDULE__SENDHOURLOCAL=8`
- `MAIL__SCHEDULE__SENDMINUTELOCAL=0`
- `MAIL__SCHEDULE__TIMEZONE=Europe/Paris`
- `MAIL__SCHEDULE__FORCE=false`

## Vérifications opérationnelles
```bash
docker compose -p probaloto-home -f deploy/home.compose.yml --env-file deploy/home.env ps
docker compose -p probaloto-home -f deploy/home.compose.yml --env-file deploy/home.env logs --tail 120 web api worker postgres
curl -fsS http://127.0.0.1:8083/health
```

## Opérations admin utiles
Synchronisation manuelle des tirages:
```http
POST /api/admin/sync
X-Api-Key: <ADMIN_API_KEY>
```

Dispatch newsletter immédiat:
```http
POST /api/admin/newsletter/dispatch
X-Api-Key: <ADMIN_API_KEY>
```

Alternative de forçage ponctuel du worker:
- passer `MAIL__SCHEDULE__FORCE=true`
- relancer `worker`
- remettre `MAIL__SCHEDULE__FORCE=false` après exécution

## Reverse proxy / exposition publique
TODO: documenter ici la configuration reverse proxy cible (domaine, TLS, upstream) selon l’infrastructure d’hébergement retenue.
