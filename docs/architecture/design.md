

## Context
### Technical context

```mermaid
graph TD
    subgraph "APPLICATION LAYER"
        B1[Leaderboard API]
    end
    
    subgraph "DATA LAYER"
        C1[PostgreSQL <br/>Source of truth]
        C2[Redis<br/>Leaderboard ranks]
    end
    
    B1 --> C1
    B1 --> C2
    
    classDef app fill:#e74c3c,stroke:#c0392b,color:#fff
    classDef db fill:#27ae60,stroke:#229954,color:#fff
    classDef cache fill:#e67e22,stroke:#d68910,color:#fff
    
    class B1 app
    class C1 db
    class C2 cache
```

## Decision records
- [Leaderboard data storage](./adr/ADR-001-Storage.md)
- [Data processing flow](./adr/ADR-002-Data-Processing.md)


## Runtime View

### Submit score
```mermaid
sequenceDiagram
    participant Client as Game Client
    participant API as API Server
    participant DB as PostgreSQL
    participant Redis as Redis
    
    Note over Client, Redis: Score Submission - Both Scenarios
    
    Client->>+API: POST /api/submit
    
    Note over API: Save to database first
    API->>+DB: Save score history + update player
    DB-->>-API: ✅ Persisted
    
    alt Redis Available (Normal Flow)
        API->>+Redis: Update leaderboard + get rankings
        Redis-->>-API: ✅ Rankings
        
        API-->>Client: HTTP 200<br/>{rank: 1247, score: 1500, topPlayers: [...], nearbyPlayers: [...]}
        Note over Client: Total: ~10ms - Full functionality
        
    else Redis Down (Outage Flow)
        API->>Redis: Try update leaderboard
        Redis-->>API: ❌ Connection failed
        
        Note over API: Skip leaderboard data
        API-->>Client: HTTP 200<br/>{rank: 0, score: 1500, topPlayers: [], nearbyPlayers: [], degraded: true}
        Note over Client: Total: ~10ms - Score saved,<br/> no leaderboard, clear outrage status
        
    end
```

### Get Leaderboard
```mermaid
sequenceDiagram
    participant Client as Game Client
    participant API as API Server
    participant DB as PostgreSQL
    participant Redis as Redis
    
    Note over Client, Redis: Get Leaderboard
    
    Client->>+API: GET /api/leaderboard?playerId=<guid>
    
    Note over API: Always get player from database
    API->>+DB: Get player by ID
    
    alt Player Exists
        DB-->>-API: ✅ Player found
        
        alt Redis Available (Normal Flow)
            API->>+Redis: Get player rank
            Redis-->>-API: ✅ Rankings
            
            API-->>Client: HTTP 200<br/>{rank: 1247, score: 1500, topPlayers: [...], nearbyPlayers: [...]}
            Note over Client: Total: ~10ms - Full leaderboard data
            
        else Redis Down (Outage Flow)
            API->>Redis: Try get leaderboard data
            Redis-->>API: ❌ Connection failed
            
            Note over API: Skip leaderboard data
            API-->>Client: HTTP 200<br/>{rank: 0, score: 1500, topPlayers: [], nearbyPlayers: [], degraded: true}
            Note over Client: Total: ~10ms - Player score only
            
        end
        
    else Player Not Found
        DB-->>API: ❌ Player not found
        API-->>Client: HTTP 404<br/>{error: "Player not found"}
        Note over Client: Player doesn't exist
        
    end
```