#!/usr/bin/env bash
set -euo pipefail

log() {
  printf '[deploy-home] %s\n' "$1"
}

error() {
  printf '[deploy-home][erreur] %s\n' "$1" >&2
}

require_env() {
  local var_name="$1"
  if [ -z "${!var_name:-}" ]; then
    error "Variable requise absente: ${var_name}"
    exit 1
  fi
}

: "${SSH_PORT:=22}"
: "${DEPLOY_REF:=main}"
: "${DEPLOY_ENVIRONMENT:=home}"
: "${GITHUB_TOKEN:=}"

require_env "SSH_HOST"
require_env "SSH_USER"
require_env "SSH_PRIVATE_KEY"
require_env "GITHUB_REPOSITORY"

# shellcheck disable=SC2153
ssh_host="$SSH_HOST"
# shellcheck disable=SC2153
ssh_user="$SSH_USER"

if [ "$DEPLOY_ENVIRONMENT" != "home" ]; then
  error "Environnement '${DEPLOY_ENVIRONMENT}' non reconnu (attendu: home)."
  exit 1
fi

log "Demarrage du deploiement ${GITHUB_REPOSITORY}@${DEPLOY_REF} vers ${ssh_user}@${ssh_host}:${SSH_PORT}."

ssh_key_file="$(mktemp)"
cleanup() {
  rm -f "$ssh_key_file"
}
trap cleanup EXIT

umask 077
printf '%s\n' "$SSH_PRIVATE_KEY" >"$ssh_key_file"
chmod 600 "$ssh_key_file"

ssh_opts=(
  -i "$ssh_key_file"
  -p "$SSH_PORT"
  -o BatchMode=yes
  -o StrictHostKeyChecking=accept-new
  -o ConnectTimeout=10
)

ssh "${ssh_opts[@]}" "${ssh_user}@${ssh_host}" \
  bash -se -- "$DEPLOY_REF" "$GITHUB_REPOSITORY" "$GITHUB_TOKEN" <<'REMOTE_SCRIPT'
set -euo pipefail

log() {
  printf '[remote] %s\n' "$1"
}

error() {
  printf '[remote][erreur] %s\n' "$1" >&2
}

require_cmd() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    error "Commande requise introuvable: ${command_name}"
    exit 1
  fi
}

dump_diagnostics() {
  log "Etat compose:"
  "${compose_cmd[@]}" -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE_PATH" ps || true

  for service_name in web api worker postgres; do
    log "Derniers logs du service ${service_name}:"
    "${compose_cmd[@]}" -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE_PATH" logs --no-color --tail 120 "$service_name" || true
  done
}

git_with_auth() {
  if [ -n "$REPO_TOKEN" ]; then
    local auth_header
    auth_header="$(printf 'x-access-token:%s' "$REPO_TOKEN" | base64 | tr -d '\n')"
    git -c "http.extraheader=AUTHORIZATION: basic ${auth_header}" "$@"
    return
  fi

  git "$@"
}

DEPLOY_REF="$1"
REPO_SLUG="$2"
REPO_TOKEN="${3:-}"
REPO_URL="https://github.com/${REPO_SLUG}.git"

APP_DIR="/home/arnaud/apps/proba-loto-euromillions"
COMPOSE_FILE="deploy/home.compose.yml"
COMPOSE_PROJECT="probaloto-home"
HEALTH_URL="http://127.0.0.1:8083/health"
HEALTH_TIMEOUT_SECONDS=300
HEALTH_POLL_SECONDS=5

APP_PARENT_DIR="$(dirname "$APP_DIR")"
COMPOSE_FILE_PATH="${APP_DIR}/${COMPOSE_FILE}"

require_cmd git
require_cmd docker
require_cmd curl

compose_cmd=()
if docker compose version >/dev/null 2>&1; then
  compose_cmd=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
  compose_cmd=(docker-compose)
else
  error "docker compose est introuvable (plugin Docker ou binaire docker-compose requis)."
  exit 1
fi

log "Preparation du dossier ${APP_DIR}"
mkdir -p "$APP_PARENT_DIR"

if [ ! -d "$APP_DIR/.git" ]; then
  log "Depot absent, clonage initial."
  if ! git_with_auth clone "$REPO_URL" "$APP_DIR"; then
    error "Clonage impossible. Si le depot est prive, verifier GITHUB_TOKEN."
    exit 1
  fi
fi

cd "$APP_DIR"
git remote set-url origin "$REPO_URL"

log "Mise a jour Git et resolution de la reference ${DEPLOY_REF}"
git_with_auth fetch --prune --tags origin

resolved_ref=""

if [[ "$DEPLOY_REF" =~ ^[0-9a-fA-F]{7,40}$ ]]; then
  if ! git rev-parse --quiet --verify "${DEPLOY_REF}^{commit}" >/dev/null; then
    error "SHA introuvable dans le depot: ${DEPLOY_REF}"
    exit 1
  fi

  log "Reference detectee comme SHA, checkout detache."
  resolved_ref="$DEPLOY_REF"
elif git rev-parse -q --verify "refs/tags/${DEPLOY_REF}" >/dev/null; then
  log "Reference detectee comme tag, checkout detache."
  resolved_ref="refs/tags/${DEPLOY_REF}"
elif git rev-parse -q --verify "refs/remotes/origin/${DEPLOY_REF}" >/dev/null; then
  log "Reference detectee comme branche, alignement sur origin/${DEPLOY_REF}."
  resolved_ref="refs/remotes/origin/${DEPLOY_REF}"
else
  error "Reference introuvable: ${DEPLOY_REF} (branche, tag ou SHA)."
  exit 1
fi

git checkout --detach "$resolved_ref"

deployed_commit="$(git rev-parse --short HEAD)"
deployed_host="$(hostname -f 2>/dev/null || hostname 2>/dev/null || echo 'unknown-host')"
deployed_date_utc="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
log "Commit deploye: ${deployed_commit}"
log "Contexte: host=${deployed_host}, date_utc=${deployed_date_utc}, ref=${DEPLOY_REF}"

if [ ! -f "$COMPOSE_FILE_PATH" ]; then
  error "Fichier compose introuvable: ${COMPOSE_FILE_PATH}"
  exit 1
fi

if docker ps -a --format '{{.Names}}' | grep -Fxq 'loto'; then
  log "Suppression du conteneur historique loto pour liberer le port 8083."
  docker rm -f loto >/dev/null || true
fi

log "Build et demarrage de la stack home via docker compose"
"${compose_cmd[@]}" -p "$COMPOSE_PROJECT" -f "$COMPOSE_FILE_PATH" up -d --build --remove-orphans

max_attempts=$((HEALTH_TIMEOUT_SECONDS / HEALTH_POLL_SECONDS))
if [ "$max_attempts" -lt 1 ]; then
  max_attempts=1
fi

http_status=""
for ((attempt=1; attempt<=max_attempts; attempt+=1)); do
  http_status="$(
    curl -sS -o /dev/null -w '%{http_code}' \
      --connect-timeout 2 \
      --max-time 5 \
      "$HEALTH_URL" || true
  )"

  if [ "$http_status" = "200" ]; then
    log "Healthcheck OK sur ${HEALTH_URL} (tentative ${attempt}/${max_attempts})."
    break
  fi

  log "En attente de ${HEALTH_URL} (tentative ${attempt}/${max_attempts}, code=${http_status:-n/a})."
  sleep "$HEALTH_POLL_SECONDS"
done

if [ "$http_status" != "200" ]; then
  error "Healthcheck en echec apres ${HEALTH_TIMEOUT_SECONDS}s (dernier code=${http_status:-n/a})."
  dump_diagnostics
  exit 1
fi

log "Deploiement termine avec succes."
REMOTE_SCRIPT

log "Script termine."
