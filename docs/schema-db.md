# Schema de base de donnees

Diagramme simplifie (PostgreSQL):

```mermaid
erDiagram
    DRAWS {
        uuid Id PK
        string Game
        date DrawDate
        int[] MainNumbers
        int[] BonusNumbers
        string Source
        timestamptz CreatedAtUtc
        timestamptz UpdatedAtUtc
    }

    SUBSCRIPTIONS {
        uuid Id PK
        citext Email
        string Game
        int GridCount
        string Strategy
        string Status
        timestamptz CreatedAt
        timestamptz ConfirmedAt
        timestamptz UnsubscribedAt
        string ConfirmTokenHash
        string UnsubTokenHash
        date LastSentForDrawDate
    }

    EMAIL_SEND_LOGS {
        uuid Id PK
        uuid SubscriptionId FK
        date IntendedDrawDate
        timestamptz SentAt
        string Status
        text Error
    }

    SYNC_RUNS {
        uuid Id PK
        string Game
        string Status
        timestamptz StartedAtUtc
        timestamptz FinishedAtUtc
        int DrawsUpsertedCount
        text Error
    }

    SYNC_STATE {
        string Game PK
        timestamptz LastSuccessfulSyncAtUtc
        date LastKnownDrawDate
        string HistoryPageEtag
        timestamptz HistoryPageLastModifiedUtc
        text CachedArchivesJson
    }

    SUBSCRIPTIONS ||--o{ EMAIL_SEND_LOGS : "a des envois"
```

Notes:
- `draws` est unique par `(Game, DrawDate)`.
- `sync_state` contient l'etat courant par jeu (dont cache HTTP FDJ).
- `email_send_logs` trace les envois effectifs et les echecs par abonnement.
