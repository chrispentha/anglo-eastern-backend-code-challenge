# Testing Guide

This guide takes you from a clean clone to a verified system in about 10 minutes:
1. Start the stack.
2. Try the API in Swagger.
3. Run the automated tests.
4. Inspect the real SQL Server database with DBeaver.

## 0. Prerequisites

| Tool | Notes |
|---|---|
| **Docker Desktop** | Must be running. On Apple Silicon, enable *Settings → General → "Use Rosetta for x86/amd64 emulation"*, because SQL Server has no ARM64 image. |
| **.NET 8 SDK** | Only needed to run the automated tests (step 4). |
| **DBeaver** (Community edition) | Only needed to inspect the database (step 5). |

> **Tip:** use `127.0.0.1` instead of `localhost` in URLs. If another app on your machine listens on `localhost:8080` over IPv6, `localhost` may reach that app instead of this API.

---

## 1. Start everything

From the repository folder:

```bash
cp .env.example .env            # optionally change the passwords
docker compose down -v          # optional: start from a clean database
docker compose up -d --build    # SQL Server -> db-init (schema + sample data) -> API
```

Wait about a minute on the first start, then check readiness:

```bash
curl http://127.0.0.1:8080/health/ready     # → Healthy
```

If the API uses another port, set `API_PORT` in `.env` and use that port in the URLs below.

## 2. Get your API keys

On the first deployment, `db-init` generates a **random key for each sample user** and prints the keys **once**. Only their hashes are stored.

```bash
docker compose logs db-init
```

```
================ LOCAL DEVELOPMENT API KEYS (shown once, expire in 180 days) ================
Administrator       alex.morgan@example.com     sm_devadm01_<random>
CrewingOfficer      priya.nair@example.com      sm_devcrw01_<random>
...
```

Copy at least the **Administrator** (`sm_devadm01_…`) and **CrewingOfficer** (`sm_devcrw01_…`) keys.

| User (id) | Role | Assigned ships |
|---|---|---|
| Alex Morgan (1) | Administrator | all ships |
| Priya Nair (2) | CrewingOfficer | SHIP01, SHIP02, SHIP04 |
| Daniel Chen (3) | Accountant | SHIP02, SHIP03 |
| Sofia Andreou (4) | OwnerRepresentative | SHIP01 |
| Marco Rossi (5) | Superintendent | SHIP03, SHIP05 |
| Hannah Lee (6) | FleetManager | SHIP01, SHIP02, SHIP03 |

**Lost the keys?** Issue new ones. They are printed in the output of this command, and the old keys are revoked:

```bash
docker compose run --rm -e ROTATE_DEV_KEYS=1 db-init
```

> You don't need the `/users/{id}/api-keys` endpoints to test. Every request just carries your key in the `X-Api-Key` header. Those endpoints are for an administrator to **issue** keys to new users and **revoke** keys.

## 3. Try the API in Swagger

Open **http://127.0.0.1:8080/swagger**. Click **Authorize**, paste a key, then click **Authorize** again.

| # | Key | Request | Expected |
|---|---|---|---|
| 1 | none | any endpoint | **401** `UNAUTHORIZED` |
| 2 | Crewing | `GET /api/v1/users/me/ships` | SHIP01, SHIP02, SHIP04 with status |
| 3 | Crewing | `GET /api/v1/ships/SHIP01/crew` | Onboard + Relief Due crew, sorted by rank seniority (Master first) |
| 4 | Crewing | `GET /api/v1/ships/SHIP01/crew?search=05 Apr` | crew who signed on 05 Apr (e.g. CREW001) |
| 5 | Crewing | `GET /api/v1/ships/SHIP01/crew?sortBy=age&sortDirection=desc&pageSize=5` | 5 oldest crew, with `totalCount` / `totalPages` |
| 6 | Crewing | `GET /api/v1/ships/SHIP03/crew` | **404** `SHIP_NOT_FOUND` (not assigned, indistinguishable from missing) |
| 7 | Crewing | `GET /api/v1/ships/SHIP04/crew` | **409** `SHIP_INACTIVE` |
| 8 | Crewing | `GET /api/v1/ships/SHIP02/financial-reports/detail?period=2025-02` | `ytdStart` **2024-04**, `ytdEnd` 2025-02 (April–March fiscal year) |
| 9 | Crewing | `GET /api/v1/ships/SHIP02/financial-reports/summary?period=2025-02` | parent accounts + GRAND TOTAL, matching the detail report |
| 10 | Crewing | `GET /api/v1/ships/SHIP01/financial-reports/detail?period=2025-13` | **400** `VALIDATION_FAILED`, `errors.period` |
| 11 | Crewing | `POST /api/v1/ships` | **403** `FORBIDDEN` (administrator only) |
| 12 | Admin | `POST /api/v1/ships` with `{"shipCode":"SHIP06","shipName":"Nautilus","fiscalYearCode":"0403"}` | **201** with a `Location` header |
| 13 | Admin | same request again | **409** `SHIP_CODE_CONFLICT` |
| 14 | Admin | `PUT /api/v1/users/2/ships/SHIP06` | **201**; repeat it → **204** (idempotent) |
| 15 | Admin | `POST /api/v1/users` with `{"fullName":"Jane Doe","role":"Accountant","isAdmin":true}` | **400**: unknown field rejected (mass-assignment protection) |

**Optimistic concurrency (ETag):**
1. Admin: `GET /api/v1/ships/SHIP06`. Note the `ETag` response header, e.g. `"00000000000007D3"`.
2. `PATCH /api/v1/ships/SHIP06` with `If-Match: "00000000000007D3"` and body `{"shipName":"Nautilus II"}` → **200**, with a new ETag.
3. Repeat step 2 with the **old** ETag → **412** `PRECONDITION_FAILED`. The second writer can't silently overwrite the first.

**Circuit breaker (optional):**
- Run `docker compose stop sqlserver` and call any endpoint a few times. The first call waits for the connection timeout; the next ones return **503** `DATABASE_UNAVAILABLE` in milliseconds, with a `Retry-After` header.
- Run `docker compose start sqlserver`. The API recovers on its own within a few seconds.

## 4. Run the automated tests

```bash
dotnet test tests/ShipManagement.UnitTests           # 240 tests, about 1 second, no database
dotnet test tests/ShipManagement.IntegrationTests    # 172 tests; starts its own SQL Server 2022 in Docker
```

The integration tests never touch your demo database. Each run uses a throwaway container built with the same `database/deploy.sh`.

**Coverage** (optional; the gate requires ≥ 90% lines and branches per project):

```bash
dotnet test ShipManagement.sln --settings coverage.runsettings --collect "XPlat Code Coverage" --results-directory TestResults
python3 scripts/coverage_gate.py TestResults 90
```

## 5. Inspect the real SQL Server with DBeaver

### Connect

New Database Connection → **SQL Server**:

| Field | Value |
|---|---|
| Host | `127.0.0.1` |
| Port | `1433` (or `SQL_PORT` from `.env`) |
| Database | `ShipManagement` |
| Authentication | SQL Server Authentication |
| Username | `sa` |
| Password | `MSSQL_SA_PASSWORD` from `.env` (default `ChangeMe_Sa_Str0ng!`) |

On the **Driver properties** tab, set `encrypt` = `true` and `trustServerCertificate` = `true`. The local container uses a self-signed certificate. Let DBeaver download the Microsoft driver when it asks, then click **Test Connection**.

### What to look at

| What | Where / query |
|---|---|
| Tables, constraints, indexes | `ShipManagement → Schemas → dbo → Tables` |
| API stored procedures | `ShipManagement → Schemas → app → Procedures` |
| ER diagram | right-click the database → **View Diagram** (also in [`docs/erd.md`](erd.md)) |
| Crew list procedure | `EXEC app.usp_Crew_ListByShip @ShipCode = 'SHIP01', @RequestedByUserId = 1;` |
| Crew list pinned to a date | `EXEC app.usp_Crew_ListByShip @ShipCode = 'SHIP01', @RequestedByUserId = 1, @AsOfDate = '2025-08-04';` |
| Financial report procedure | `EXEC app.usp_FinancialReport_Summary @ShipCode = 'SHIP02', @Period = '2025-02-01', @RequestedByUserId = 1;` |
| Audit trail of your Swagger calls | `SELECT TOP 20 * FROM dbo.AuditLog ORDER BY AuditLogId DESC;` |
| Audit rows are tamper-proof | `DELETE FROM dbo.AuditLog;` → error `AUDIT_IMMUTABLE` |
| Only hashes of API keys are stored | `SELECT ApiKeyId, UserId, KeyPrefix, KeyHash, ExpiresAtUtc, RevokedAtUtc FROM dbo.ApiKey;` |
| Child-account rule | try inserting a budget for parent account `7100000` → FK error: postings go to child accounts only |

### Security proof: the API's own login

Create a **second connection** with username `ship_api` and password `APP_DB_PASSWORD` from `.env`:

```sql
SELECT * FROM dbo.Ship;        -- ❌ The SELECT permission was denied (the API can't read tables directly)
EXEC app.usp_Health_Ping;      -- ✅ works (the API can only execute procedures in schema [app])
```

### Without DBeaver (terminal)

```bash
docker compose exec sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d ShipManagement'
```

Type SQL, then `GO` on its own line to run it, and `exit` to quit. This is fine for quick checks; DBeaver is better for presenting.

## 6. Suggested demo order (presentation)

1. **Swagger:** crew list, fiscal-year report, 404 / 409 / 412 responses.
2. **DBeaver as `sa`:** ER diagram, a table's constraints, a procedure's code, the audit rows your calls created.
3. **DBeaver as `ship_api`:** `SELECT` denied, `EXEC` allowed. That's the least-privilege design in 10 seconds.
4. **Terminal:** `dotnet test` with every test passing, plus the coverage gate.

## 7. Stop

```bash
docker compose down        # stop and keep the data
docker compose down -v     # stop and wipe the database (the next start re-seeds with new keys)
```

## Troubleshooting

| Problem | Fix |
|---|---|
| `{"error":"not found"}` or an unexpected page on `localhost:8080` | another app owns `localhost:8080`: use `127.0.0.1:8080`, or change `API_PORT` in `.env` |
| API keys missing from `docker compose logs db-init` | the `db-init` container was recreated: run `docker compose run --rm -e ROTATE_DEV_KEYS=1 db-init` |
| An old key returns 401 | it was revoked by a rotation: use the newly printed keys |
| DBeaver SSL / certificate error | set `trustServerCertificate` = `true` in Driver properties |
| SQL Server container keeps restarting on Apple Silicon | enable Rosetta emulation in Docker Desktop and restart Docker |
| Integration tests fail to start | Docker must be running; the first run pulls the SQL Server image (about 1.5 GB) |
