# Guide de deploiement Docker (production)

## 1. Prerequis
- Docker Engine + plugin Compose.
- DNS et certificat TLS geres en amont (reverse proxy ou load balancer).
- Variables secretes disponibles (fichier `.env` securise ou coffre-fort).

## 2. Variables minimales
Exemple de fichier `ops/.env.prod`:

```dotenv
POSTGRES_USER=probaloto
POSTGRES_PASSWORD=mot-de-passe-fort
ADMIN_API_KEY=cle-api-admin-forte
ADMIN_WEB_USERNAME=admin
ADMIN_WEB_PASSWORD=mot-de-passe-ui-fort

SMTP_HOST=smtp.votre-fournisseur.tld
SMTP_PORT=587
SMTP_USER=utilisateur-smtp
SMTP_PASS=mot-de-passe-smtp
SMTP_FROM=Probabilites Loto <no-reply@votre-domaine.tld>
SMTP_USE_STARTTLS=true

PUBLIC_BASE_URL=https://votre-domaine.tld
SUBSCRIPTIONS_TOKEN_SECRET=secret-long-aleatoire
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
  - `SUBSCRIPTIONS_TOKEN_SECRET`
  - secrets SMTP;
- sauvegardes regulieres du volume `postgres_data`;
- supervision de `/health` + traces/metriques OTEL.

## 6. Mise a jour applicative
```powershell
docker compose --env-file .env.prod -f docker-compose.prod.yml pull
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
```

Les migrations EF sont appliquees automatiquement au demarrage de l'API/Worker si `Database:AutoMigrate=true` (valeur par defaut).
