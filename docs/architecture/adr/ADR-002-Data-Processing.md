# ADR-004: Data Processing and Storage Strategy

**Status:** Accepted | **Date:** 2025-07-19

## Problem
How should we process and store leaderboard data to ensure both persistence and performance at scale?

## Decision
**Database-first persistence with Redis for real-time operations**: All data writes go to PostgreSQL first, then Redis for fast leaderboard queries.

## Data Flow Strategy

### Write Operations
1. **Save to PostgreSQL** (players + score history)
2. **Update Redis** (leaderboard rankings) 
3. **Return response** from Redis data

### Read Operations
- **Player data**: PostgreSQL (current state)
- **Leaderboard queries**: Redis only (sorted sets)
- **Historical data**: PostgreSQL (audit trail)

## Why
- **Data durability**: PostgreSQL ensures no data loss
- **Query performance**: Redis provides sub-millisecond leaderboard operations
- **Audit compliance**: Complete history in PostgreSQL
- **Scalability**: Redis handles millions of concurrent ranking queries

## Redis Outage Handling
- **Score submission**: Continue saving to database, return empty leaderboard
- **Get leaderboard**: Return only player score with empty leaderboard and error indicator. Calculating data directly from database doesn't viable for system with million users. It takes too much time. If we can make sure the system can always back to normal shortly after outrage, this is the prefered approach.

## Trade-offs
- ✅ Zero data loss with database-first approach
- ✅ Sub-millisecond leaderboard performance via Redis
- ✅ Complete audit trail in PostgreSQL
- ✅ Simple failure handling (empty responses)
- ❌ No leaderboard functionality during Redis outages
- ❌ Slight complexity managing two storage systems