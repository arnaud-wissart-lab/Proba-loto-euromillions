# Guide de deploiement Docker (production)

## 1. Prerequis
- Docker Engine + plugin Compose.
- DNS et certificat TLS geres en amont (reverse proxy ou load balancer).
- Variables secretes disponibles (fichier `.env` securise ou coffre-fort).

Mode home (runner self-hosted): le fichier a renseigner est `deploy/home.env` sur la machine cible (`/home/arnaud/apps/proba-loto-euromillions/deploy/home.env`).

## 2. Variables minimales
Exemple de fichier `ops/.env.prod`:

```dotenv
POSTGRES_USER=probaloto
POSTGRES_PASSWORD=mot-de-passe-fort
ADMIN_API_KEY=cle-api-admin-forte
ADMIN_WEB_USERNAME=admin
ADMIN_WEB_PASSWORD=mot-de-passe-ui-fort

MAIL__ENABLED=true
MAIL__FROM=no-reply@votre-domaine.tld
MAIL__FROMNAME=Probabilites Loto
MAIL__BASEURL=https://votre-domaine.tld
MAIL__SMTP__HOST=smtp-relay.brevo.com
MAIL__SMTP__PORT=587
MAIL__SMTP__USESSL=true
MAIL__SMTP__USERNAME=utilisateur-smtp
MAIL__SMTP__PASSWORD=mot-de-passe-smtp
MAIL__SCHEDULE__SENDHOURLOCAL=8
MAIL__SCHEDULE__SENDMINUTELOCAL=0
MAIL__SCHEDULE__TIMEZONE=Europe/Paris
MAIL__SCHEDULE__FORCE=false
```

## 3. Demarrage
Depuis `ops/`:

```powershell
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
```

## 4. Verification post-deploiement
- Web: `http(s)://<votre-domaine>/`
- Health API (interne ou expose de facon restreinte): `/health`
- Page admin: `/admin` (authentification Basic)
- Logs: `docker compose -f docker-compose.prod.yml logs -f api worker web`

## 5. Bonnes pratiques recommandees
- forcer TLS sur toutes les entrees HTTP;
- ne pas exposer PostgreSQL sur Internet;
- rotation periodique:
  - `ADMIN_API_KEY`
  - `ADMIN_WEB_PASSWORD`
  - secrets SMTP (`MAIL__SMTP__USERNAME`, `MAIL__SMTP__PASSWORD`);
- sauvegardes regulieres du volume `postgres_data`;
- supervision de `/health` + traces/metriques OTEL.

## 6. Mise a jour applicative
```powershell
docker compose --env-file .env.prod -f docker-compose.prod.yml pull
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
```

Les migrations EF sont appliquees automatiquement au demarrage de l'API/Worker si `Database:AutoMigrate=true` (valeur par defaut).

## 7. RUNBOOK - Forcer un envoi newsletter
Deux options sont disponibles.

### Option A - Flag de configuration
1. Mettre `MAIL__SCHEDULE__FORCE=true` dans `deploy/home.env`.
2. Relancer le worker (ou la stack) pour prendre en compte la variable.
3. Verifier les logs worker (`sent/skipped/errors` agreges).
4. Remettre `MAIL__SCHEDULE__FORCE=false` apres le run force.

Notes:
- le mode force contourne la contrainte horaire, mais conserve les regles de jours de tirage;
- l'idempotence est preservee via `mail_dispatch_history`.

### Option B - Endpoint admin protege API key
Appeler:

```http
POST /api/admin/newsletter/dispatch
X-Api-Key: <ADMIN_API_KEY>
```

Cette route declenche immediatement un dispatch avec `force=true` et retourne un resume agregé.
