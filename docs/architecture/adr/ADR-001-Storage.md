# ADR-001: Leaderboard Data Storage

**Status:** Accepted | **Date:** 2025-07-19

## Problem
Need real-time leaderboard for millions of players with sub-millisecond response times, plus historical data storage. Must be able to handle complex ranking algorithms and dramatic rank changes efficiently.

## Decision
**Redis Sorted Sets** for live leaderboard + **PostgreSQL** for historical submissions.

## Why
- **Redis**: O(log N) operations, <1ms latency, atomic updates, handles rank changes automatically
- **PostgreSQL**: Cost-effective historical storage, familiar operations
- **Rejected Elasticsearch**: 10-50x slower (10-50ms) for simple ranking operations

## Ranking Complexity Considerations
- **Dynamic rankings**: Consider Elasticsearch if algorithms change frequently

## Trade-offs
- ✅ Sub-millisecond performance even with dramatic rank changes
- ✅ Scales to 100M+ players with complex scoring
- ✅ Automatic rank recalculation in O(log N) time
- ❌ Dual-store complexity
- ❌ Ranking complexity must be pre-computed into single score