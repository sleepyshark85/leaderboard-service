# LEADERBOARD SERVICE

## Documentation
- [Architecture design documentation](./docs/architecture/design.md)
- Important decisions are documented [here](./docs/architecture//adr/)

## How to run

### Prerequisites
Docker engine is required to run this application.

### Run
Execute the follow command at the root of the repository

```
docker-compose up --build -d
```

### Test
The repository comes with a few simple system test cases. Make sure all containers are up and running before execute the tests

```
dotnet test .\tests\PS.LeaderboardAPI.Tests\PS.LeaderboardAPI.Tests.csproj
```

### Access the application
- Leaderboard API: http://localhost:5108/scalar
- PostgreSQL: http://localhost:8080/?pgsql=leaderboard_postgres&username=postgres&db=leaderboard&ns=public
    - User: postgres
    - Password: dev_password
-  Redis: http://localhost:5540