# ADR 0001 - Observabilite, administration minimale et robustesse d'ingestion

## Statut
Acceptee

## Contexte
Le socle doit fournir:
- une observabilite exploitable en exploitation (logs structures, traces, metriques, health checks);
- une administration minimale sans ajouter de systeme IAM complexe;
- une ingestion FDJ resiliente aux evolutions mineures de structure HTML/CSV;
- une reduction de trafic inutile vers les pages historiques FDJ.

## Decision
1. Observabilite:
- Serilog est configure en JSON sur `Api`, `Worker` et `Web`.
- OpenTelemetry conserve l'instrumentation HTTP/runtime et ajoute le meter `ProbabilitesLotoEuroMillions.DrawSync`.
- L'API expose des checks de readiness `postgres` et `smtp` (SMTP optionnel via `HealthChecks:Smtp:Enabled`).

2. Administration minimale:
- Les endpoints admin API restent proteges par `X-Api-Key`.
- Un endpoint `GET /api/admin/sync-runs` est ajoute pour consulter l'historique.
- La page Web `/admin` est protegee par authentification HTTP Basic et permet:
  - consultation des `SyncRuns`;
  - declenchement "Sync maintenant".

3. Robustesse ingestion:
- Le parsing HTML accepte des variations mineures sur labels/URLs (heuristiques plus souples).
- Le parsing CSV/Excel supporte des alias de colonnes et un fallback par tokens.
- Le cache conditionnel FDJ utilise `If-None-Match` / `If-Modified-Since`.
- Les metadonnees HTTP et la derniere liste d'archives sont persistees dans `sync_state`.

## Consequences
Positives:
- meilleure supervision (logs + traces + metriques + readiness);
- admin operationnelle avec cout de mise en oeuvre faible;
- ingestion moins fragile aux changements cosmetiques des sources;
- baisse des transferts inutiles quand la page historique ne change pas.

Risques et limites:
- l'authentification Basic est minimale et doit etre placee derriere TLS en production;
- le cache d'archives est par jeu (pas par fichier), ce qui reste volontairement simple.

## Alternatives ecartees
- IAM complet (OIDC/Identity): trop lourd pour le besoin actuel.
- suppression de la synchro manuelle: refusee pour l'operabilite.
- cache HTTP en memoire uniquement: insuffisant apres redemarrage.
