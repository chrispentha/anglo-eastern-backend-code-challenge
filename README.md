# Ship Management Backend — AE Backend Code Challenge

A ship-management backend: a **SQL Server** database (schema, inline table-valued functions, stored procedures, sample data) and a **C# / ASP.NET Core 8** REST API that reaches the data **only through stored procedures**. It covers users, ships, ship assignments, crew lists and fiscal-year-aware financial expense reports. **412 automated xUnit tests** run against a real SQL Server 2022, with **≥ 90% line and branch coverage in every project** (enforced in CI).

Every item in the brief is implemented, including all the "nice to haves": API-key authentication with pluggable JWT, table-valued functions, logging and an audit trail of key actions, Swagger UI, a one-command Docker environment, and a GitHub Actions pipeline.

| Brief requirement | Where |
|---|---|
| ERD, DDL, constraints | [`docs/erd.md`](docs/erd.md), [`database/02–04_*.sql`](database) |
| Sample data (5 ships, ≥ 20 crew each, COA, budgets, actuals) | [`database/08_sample_data.sql`](database/08_sample_data.sql), checked by [`database/tests/verify_sample_data.sql`](database/tests/verify_sample_data.sql) |
| Crew list SP (paging, sorting, search incl. `05 Apr`) | [`app.usp_Crew_ListByShip`](database/procedures/app.usp_Crew_ListByShip.sql) |
| Financial reports (detail + summary, fiscal YTD, variances) | [`dbo.tvf_FinancialLines`](database/05_functions.sql), [`dbo.usp_FinancialReport_Build`](database/procedures/dbo.usp_FinancialReport_Build.sql) |
| REST API (users, ships, assignments, crew, reports) | [`src/`](src) |
| xUnit tests | [`tests/`](tests): 240 unit + 172 integration, coverage gate ≥ 90% |
| Auth, TVFs, logging, Swagger, Docker, CI | see [Security](#security), [Database](#database-design), [Operations](#operations), [`docker-compose.yml`](docker-compose.yml), [`.github/workflows/ci.yml`](.github/workflows/ci.yml) |

---

## Contents

1. [Quick start](#quick-start)
2. [Architecture](#architecture)
3. [Database design](#database-design)
4. [Business rules and how they are implemented](#business-rules-and-how-they-are-implemented)
5. [API reference](#api-reference)
6. [Security](#security)
7. [Testing](#testing)
8. [Operations](#operations)
9. [Decisions register](#decisions-register)
10. [Notes on the brief](#notes-on-the-brief)
11. [Production hardening and future work](#production-hardening-and-future-work)

---

## Quick start

**Prerequisites:** Docker Desktop (or Docker Engine with Compose v2). On Apple Silicon, enable *"Use Rosetta for x86/amd64 emulation"*, because SQL Server has no ARM64 image.

```bash
cp .env.example .env            # optionally change the passwords
docker compose up -d --build    # SQL Server -> db-init (schema + sample data) -> API
```

| What | URL |
|---|---|
| Swagger UI | http://localhost:8080/swagger |
| Readiness probe | http://localhost:8080/health/ready |
| API base | http://localhost:8080/api/v1 |

The first start takes about a minute while SQL Server initialises. `db-init` runs [`database/deploy.sh`](database/deploy.sh) and exits. The API starts only after `db-init` succeeds.

### Local development API keys

The sample data creates one user per role. On the first deployment, `db-init` generates a **random API key for each of them** with SQL Server's cryptographic random generator. It stores only the SHA-256 hash and **prints the keys once**:

```bash
docker compose logs db-init
```

```
================ LOCAL DEVELOPMENT API KEYS (shown once, expire in 180 days) ================
Administrator       alex.morgan@example.com     sm_devadm01_<random>
CrewingOfficer      priya.nair@example.com      sm_devcrw01_<random>
...
```

No key is stored in this repository. Later deployments keep the existing keys. To revoke them and issue new ones (for example, if you lost them), run the command below. The new keys are printed **in that command's output**; `docker compose logs db-init` keeps showing the original, now revoked, keys.

```bash
docker compose run --rm -e ROTATE_DEV_KEYS=1 db-init
```

The keys exist only when `SEED_SAMPLE_DATA=1`, and they expire after 180 days. Never enable sample data in a real environment.

In Swagger UI, click **Authorize** and paste a key, or send it in the `X-Api-Key` header.

| User (id) | Role | Assigned ships | Key prefix |
|---|---|---|---|
| Alex Morgan (1) | Administrator | all (administrator) | `sm_devadm01_` |
| Priya Nair (2) | CrewingOfficer | SHIP01, SHIP02, SHIP04 | `sm_devcrw01_` |
| Daniel Chen (3) | Accountant | SHIP02, SHIP03 | `sm_devacc01_` |
| Sofia Andreou (4) | OwnerRepresentative | SHIP01 | `sm_devown01_` |
| Marco Rossi (5) | Superintendent | SHIP03, SHIP05 | `sm_devsup01_` |
| Hannah Lee (6) | FleetManager | SHIP01, SHIP02, SHIP03 | `sm_devflt01_` |

### Try it

```bash
# Read the generated keys from the db-init output
key() { docker compose logs db-init --no-log-prefix | grep -o "sm_$1_[A-Za-z0-9_-]\{43\}" | tail -1; }
ADMIN=$(key devadm01)
CREW=$(key devcrw01)

# Crew list: onboard + relief due, sorted by rank seniority, searched by partial date
curl -s -H "X-Api-Key: $CREW" "http://localhost:8080/api/v1/ships/SHIP01/crew?search=05%20Apr"

# Detail report for an April-March ship: YTD runs Apr 2024 - Feb 2025
curl -s -H "X-Api-Key: $CREW" "http://localhost:8080/api/v1/ships/SHIP02/financial-reports/detail?period=2025-02"

# Not assigned -> 404 (indistinguishable from a missing ship); inactive ship -> 409 SHIP_INACTIVE
curl -s -H "X-Api-Key: $CREW" "http://localhost:8080/api/v1/ships/SHIP03/crew"
curl -s -H "X-Api-Key: $CREW" "http://localhost:8080/api/v1/ships/SHIP04/crew"

# Create a ship and assign it (administrator)
curl -s -X POST -H "X-Api-Key: $ADMIN" -H "Content-Type: application/json" \
     -d '{"shipCode":"SHIP06","shipName":"Nautilus","fiscalYearCode":"0403"}' http://localhost:8080/api/v1/ships
curl -s -X PUT -H "X-Api-Key: $ADMIN" http://localhost:8080/api/v1/users/2/ships/SHIP06 -w "%{http_code}\n"
```

More requests are in [`src/ShipManagement.Api/ShipManagement.Api.http`](src/ShipManagement.Api/ShipManagement.Api.http).

**Step-by-step testing and demo guide** (Swagger scenarios, automated tests, inspecting the database with DBeaver): [`docs/TESTING_GUIDE.md`](docs/TESTING_GUIDE.md).

### Running without Docker Compose

- **.NET 8 SDK** is required (pinned in [`global.json`](global.json)).
- Deploy the database to any SQL Server 2022, Express or Azure SQL Database with `database/deploy.sh`. It needs `sqlcmd` and the environment variables listed at the top of that script.
- Then run the API with the least-privilege connection string:

```bash
export Database__ConnectionString="Server=localhost,1433;Database=ShipManagement;User ID=ship_api;Password=...;Encrypt=True;TrustServerCertificate=True"
dotnet run --project src/ShipManagement.Api      # Development profile: Swagger at http://localhost:5145/swagger
```

### Running the tests

```bash
dotnet test tests/ShipManagement.UnitTests          # fast, no database
dotnet test tests/ShipManagement.IntegrationTests   # needs Docker: starts SQL Server 2022 via Testcontainers
```

---

## Architecture

### Layered (Clean Architecture), with stored procedures as the data boundary

```
            ┌──────────────────────────── ShipManagement.Api ─────────────────────────────┐
 HTTP ────► │ exception handler → security headers → compression → request log → routing │
            │ → authentication (X-Api-Key / JWT) → rate limiter → authorization → controller│
            └──────────────────────────────────┬──────────────────────────────────────────┘
                                               ▼
            ┌────────────────────────── ShipManagement.Application ───────────────────────┐
            │ use-case services · FluentValidation validators · DTOs · ICurrentUser      │
            │ repository interfaces                                                       │
            └───────────────┬─────────────────────────────────────▲───────────────────────┘
                            ▼                                     │ implements
            ┌─ ShipManagement.Domain ─┐        ┌──────── ShipManagement.Infrastructure ────────┐
            │ value objects,          │◄───────│ Dapper stored-procedure repositories,        │
            │ error codes, exceptions │        │ SqlErrorMapper, API-key crypto, auth cache   │
            └─────────────────────────┘        └──────────────────────┬────────────────────────┘
                                                                      ▼ EXEC app.usp_* (typed parameters)
                   ┌──────── SQL Server — login ship_api: EXECUTE on schema [app] only ────────┐
                   │ app.usp_*  ──ownership chaining──►  dbo tables  +  dbo.tvf_* inline TVFs  │
                   └───────────────────────────────────────────────────────────────────────────┘
```

| Project | Depends on | Responsibility |
|---|---|---|
| `ShipManagement.Domain` | — | Value objects (`ShipCode`, `AccountingPeriod`, sort whitelists, paging limits), error codes, exception types. Pure C#. |
| `ShipManagement.Application` | Domain | Use cases. Services validate input, apply access intent, call one repository method and shape the response. They see only interfaces. |
| `ShipManagement.Infrastructure` | Application | The only code that talks to SQL Server: `StoredProcedureExecutor`, repositories, SQL error mapping, API-key generation and hashing, the security-context cache. |
| `ShipManagement.Api` | Application, Infrastructure | HTTP concerns only: controllers, authentication and policies, problem details, rate limiting, headers, compression, Swagger, request logging. |

**Patterns used:**
- Repository: one stored procedure per repository method.
- Service layer, one service per use case area.
- Constructor dependency injection.
- Options pattern: typed configuration validated at start-up, so the API fails fast on bad config.
- Value objects: an invalid ship code or period cannot exist once parsed.
- Centralised exception-to-problem mapping: controllers contain no `try/catch`.
- Strategy for authentication: an API-key handler and a JWT handler behind one policy scheme.

**Performance and scalability:**
- **Exactly one stored-procedure call per request**, plus one for authentication only on a cache miss.
- Set-based SQL. Inline TVFs are expanded into the query plan, so they cost nothing at runtime.
- Covering and filtered indexes.
- `READ_COMMITTED_SNAPSHOT`, so report readers never block writers.
- Async I/O end to end. The request `CancellationToken` reaches SQL Server, so abandoned requests stop working.
- Pooled connections, and a 15-second command timeout.
- Bounded work: page size ≤ 100, search ≤ 100 characters, rate limiting.
- Brotli/Gzip compression for report payloads, which travel over slow ship-to-shore links.
- The API is **stateless**, so instances scale horizontally. Its only per-instance state is a 30-second security-context cache (see D-25).

**Concurrency and resilience:**
- **Race conditions:**
  - Duplicate creates are blocked by `UPDLOCK, HOLDLOCK` checks backed by `UNIQUE` constraints.
  - Assign and unassign are idempotent and serialised.
  - A change and its audit row commit together.
  - Reports read a consistent snapshot (`READ_COMMITTED_SNAPSHOT`).
  - Concurrent **ship updates use optimistic concurrency**: an `ETag`/`If-Match` pair built on `rowversion` returns 412 instead of a silent lost update (D-30).
- **Transient faults** (D-31, Polly v8 in `StoredProcedureExecutor`, the single path to the database):
  - Reads and idempotent writes are retried on transient SQL errors (deadlock victim, Azure SQL failover, dropped connection) with exponential back-off and jitter. **Writes that must not run twice are never retried.**
  - A **circuit breaker** opens when too many calls fail with outage-type errors. While it's open, requests fail fast with **503 `DATABASE_UNAVAILABLE`** plus `Retry-After`, instead of each waiting for a timeout. Business errors never count.
- **Caching** is deliberately limited to the security context (D-29): see the decisions register.

---

## Database design

Entity–relationship diagram and design notes: **[docs/erd.md](docs/erd.md)**.

**Scripts** (all re-runnable, executed in this order by [`database/deploy.sh`](database/deploy.sh)):

| Script | Purpose |
|---|---|
| `00_create_database.sql` | Database with collation `Latin1_General_100_CI_AS` (case-insensitive search), `READ_COMMITTED_SNAPSHOT ON` |
| `01_schemas.sql` | Schema `app` for API procedures (tables and internals stay in `dbo`) |
| `02_reference_tables.sql` | `FiscalYear`, `ShipStatus`, `Department`, `CrewRank`, `Country`, `AppRole` |
| `03_core_tables.sql` | `Ship`, `CrewMember`, `CrewServiceHistory`, `AppUser`, `UserShip`, `ApiKey`, `AuditLog` (append-only) |
| `04_finance_tables.sql` | `Account` (hierarchical COA), `BudgetEntry`, `AccountTransaction` |
| `05_functions.sql` | Inline TVFs (below) |
| `procedures/*.sql` | One stored procedure per file |
| `06_security.sql` | Role `app_executor` with `EXECUTE ON SCHEMA::app` only, plus the API login and user |
| `07_seed_reference.sql` | Fiscal years 0112/0403/0706/1009, statuses, 19 STCW ranks with seniority, countries, 6 roles |
| `08_sample_data.sql` | Fictional sample data (loaded once) |
| `tests/verify_sample_data.sql` | Fails the deployment if any minimum in the brief is not met |
| `09_dev_api_keys.sql` | Random local development API keys for the sample users, printed once (only with sample data) |

**Key design choices** (details in [docs/erd.md](docs/erd.md)):
- **Normalization and keys:** 3NF. Joins use surrogate `INT IDENTITY` keys. Business keys (`ShipCode`, `CrewMemberId`, `AccountNumber`) are `UNIQUE` and are the only identifiers the API exposes.
- **Types:** `NVARCHAR` for human text, `VARCHAR` for codes, `DATE` for business dates, `DATETIME2(3)` UTC for audit columns, `DECIMAL(19,2)` for money (never `FLOAT` or `MONEY`).
- **Constraints:** every constraint is named, and the rules are declarative:
  - amounts are `NOT NULL CHECK (>= 0)`;
  - an accounting period is the first day of a month;
  - fiscal-year codes must be internally consistent (`EndMonth = month before StartMonth`);
  - ship codes and crew IDs are pattern-checked.
- **Budgets and actuals can only be posted to child accounts.** A composite FK `(AccountId, AccountType='C')` enforces this, and a parent account must itself be of type `P` (persisted computed column + composite self-FK). There are no triggers and no application code involved.
- **Rank lives on `CrewServiceHistory`**, because rank is per contract and seafarers are promoted between contracts. Crew status is **never stored**; it is derived from the dates.

**Reusable inline table-valued functions** (N2). Inline, so they have no row-by-row or scalar-UDF cost:

| Function | Purpose |
|---|---|
| `tvf_CrewStatus` | Onboard / Planned / Relief Due / Signed Off from the dates (D-01) |
| `tvf_AgeInYears` | Age in completed years, including 29 February births |
| `tvf_DateLabel` | `dd MMM yyyy` built by hand, independent of session language (`CONVERT 106` depends on `SET LANGUAGE`, `FORMAT` is slow) |
| `tvf_FiscalYearStart` | First month of the fiscal year containing a period (the calendar-year crossover) |
| `tvf_UserContext`, `tvf_ShipForUser` | Active user and role flags; a ship only if the caller may access it (used by every ship-scoped procedure) |
| `tvf_AccountTree`, `tvf_AccountClosure` | COA depth, tree-order path, ancestor closure (any depth) |
| `tvf_FinancialLines` | The complete report computation, shared by Detail and Summary so they always tie |

**Stored procedures** (`app` schema is callable by the API; `dbo.usp_FinancialReport_Build` is internal):

| Area | Procedures |
|---|---|
| Auth | `usp_Auth_ResolveApiKey`, `usp_Auth_ResolveUser`, `usp_ApiKey_Create`, `usp_ApiKey_ListByUser`, `usp_ApiKey_Revoke` |
| Users | `usp_User_Create`, `usp_User_GetById`, `usp_User_List` |
| Ships | `usp_Ship_Create`, `usp_Ship_GetByCode`, `usp_Ship_List`, `usp_Ship_Update` |
| Assignments | `usp_UserShip_Assign`, `usp_UserShip_Unassign`, `usp_UserShip_ListByUser` |
| Crew | `usp_Crew_ListByShip` |
| Finance | `usp_FinancialReport_Detail`, `usp_FinancialReport_Summary` (→ `dbo.usp_FinancialReport_Build`) |
| Ops | `usp_Health_Ping` |

Every procedure follows the same rules:
- It starts with `SET NOCOUNT ON; SET XACT_ABORT ON;` and **validates every parameter**, even though the API validated them already.
- Its parameters are wider than the target columns, so over-long input is rejected instead of silently truncated.
- It uses **no dynamic SQL**.
- Write procedures use a transaction with `TRY/CATCH` and write the audit row **in the same transaction**. The concurrent-safe idempotent assign uses `UPDLOCK, HOLDLOCK`.

**Error contract (SP → HTTP):**

| `THROW` number | Meaning | HTTP |
|---|---|---|
| 50001 | Validation failed | 400 `VALIDATION_FAILED` |
| 50002 | Not found **or not accessible** | 404 (`SHIP_NOT_FOUND`, `USER_NOT_FOUND`, …) |
| 50003 | Conflict / duplicate | 409 (`SHIP_CODE_CONFLICT`, `USER_EMAIL_CONFLICT`) |
| 50004 | Ship inactive | 409 `SHIP_INACTIVE` |
| 50005 | Caller lacks the role | 403 `FORBIDDEN` |
| 50006 | Row version is stale (optimistic concurrency) | 412 `PRECONDITION_FAILED` |
| 2627 / 2601, 547 | Unique / constraint violation that slipped past validation | 409 / 400 |
| deadlock, failover, timeout, login failure… | Outage (after safe retries) | 503 `DATABASE_UNAVAILABLE` + `Retry-After` |
| anything else | Unexpected | 500 `INTERNAL_ERROR` (generic message + `traceId`) |

Messages are formatted `CODE|human message`. The API forwards only this curated text, never SQL Server's own messages.

### Sample data

All people are fictional. The data was generated deterministically.

- **5 ships:**
  - `SHIP01` Flying Dutchman (0112, Active)
  - `SHIP02` Thousand Sunny (0403, Active)
  - `SHIP03` Black Pearl (0706, Active)
  - `SHIP04` Going Merry (1009, **Inactive**)
  - `SHIP05` Queen Anne's Revenge (0112, **Inactive**)

  The inactive ships also have crew and finance data, which proves they are excluded.
- **225 crew members.**
  - **Every ship has 22–23 crew currently on board**, across 19 STCW ranks.
  - The mix covers Onboard, Onboard within 30 days after end of contract, Relief Due, Planned and Signed Off crew.
  - It includes 1-day handover overlaps, and seafarers with earlier contracts on other ships (some in a lower rank).
  - Crew dates are **relative to the day the seed runs** (D-21), so the demo always shows a realistic mix.
  - The brief's own records (CREW001 Soka Philip, CREW002 Masteros Philip, with their exact dates) are included as-is.
- **Chart of Accounts:** root `7000000 OPERATING EXPENSES` with 7 parent accounts of 5–6 children each (3 levels). It includes the brief's `7100000` and `7135000` plus maritime OPEX lines: crew costs, stores and spares, lubricating oils, repairs and maintenance, insurance, management and administration.
- **Budgets** for SHIP01–04, every month of 2024–2026. **Actuals** for all of 2024 and Jan 2025 – Aug 2026, with 1–3 transactions per period. Both include explicit zeros, duplicate budget lines, accounts with actuals but no budget and the reverse, and the brief's exact SHIP01/7135000 rows (budget 0 + 1000, actuals 300 + 0 + 700).

---

## Business rules and how they are implemented

### Crew status (D-01, D-02)
Evaluated in this order, as of **today's UTC date**:

| Order | Condition | Status | In crew list? |
|---|---|---|---|
| 1 | Sign Off date is set (past or future) | Signed Off | no |
| 2 | Sign On date is after today | Planned | no |
| 3 | Today is **more than 30 days** after End of Contract | Relief Due | yes |
| 4 | Otherwise, including 0–30 days after End of Contract and signing on today | Onboard | yes |

Why UTC: `GETDATE()` returns the *server's* local time, so the same crew member could have a different status depending on where the database is hosted. UTC gives one answer everywhere, avoids daylight-saving gaps, and matches maritime practice. Business dates themselves are plain `DATE`s and are **not** time-zone converted. The procedures accept an optional `@AsOfDate` so tests can pin "today". It is not exposed by the API.

### Crew list
- **Paging:** `pageNumber ≥ 1`, `pageSize 1–100` (default 20). The response includes `totalCount` and `totalPages`. A page beyond the end is empty, with 200.
- **Sorting:** `rank` (default), `crewMemberId`, `firstName`, `lastName`, `age`, `nationality`, `signOnDate` or `status`, in either direction.
  - **`rank` sorts by seniority** (Master → … → Wiper), not alphabetically.
  - Sort keys are whitelisted and implemented as a static `ORDER BY CASE` (one expression per key, type and direction).
  - `CrewMemberId` is always the final tie-breaker, so pages are stable.
- **Search:** one case-insensitive *contains* term, matched against rank, crew ID, first and last name, age, nationality and the `dd MMM yyyy` sign-on label. So `05 Apr`, `apr 2025` and `phil` all work. **Status is never searched.** The wildcards `%`, `_` and `[` are escaped, so they match literally. The term is limited to 100 characters.
- **Output:** Rank Name, Crew Member ID, First Name, Last Name, Age, Nationality, SignOnDate (ISO) plus `signOnDateLabel` (`05 Apr 2025`), and Status. The birth date is not exposed, only the age.

### Financial reports
For one ship and one accounting period (`yyyy-MM`):

- **YTD** runs from the first month of the ship's fiscal year that contains the period, up to and including the period:
  - 0112 with Jul 2025 → **Jan–Jul 2025**
  - 0403 with Feb 2025 → **Apr 2024 – Feb 2025** (crosses the calendar year)
  - 0403 with Apr 2025 → Apr 2025 only
- **Child accounts** sum all their budget lines and transactions in the period and in the YTD window. **Parent accounts** are the sum of *all descendant* child accounts, at any depth, through the account closure.
- **0 ≠ NULL (D-06):**
  - A cell is `null` when no rows exist, meaning no data.
  - It is `0` when rows exist and sum to zero.
  - Variance is `Actual − Budget`, treating a missing side as 0. It is `null` only when both sides are `null`.
  - The API never turns `null` into `0`.
- **Row inclusion (D-07):** a row is shown if any of Actual, Budget, Actual YTD or Budget YTD is non-null and non-zero. Amounts are never negative, so a parent appears exactly when at least one descendant does.
- **Detail** lists every qualifying account (parents and children) in tree order, with level and parent. **Summary** lists the qualifying **parent (summary) accounts** (D-09). Both end with a **GRAND TOTAL** row. Both come from the same TVF, so every summary row is identical to the matching detail row. A test asserts this.
- The response includes `ytdStart`, `ytdEnd` and ready-made column labels such as `"Actual YTD (Apr 2024 - Feb 2025)"`.
- An **inactive ship** returns `409 SHIP_INACTIVE` for crew lists and reports. It still appears, with its status, in ship lists and in "my ships".

---

## API reference

Base path `/api/v1`, JSON (camelCase). Every endpoint except `/health*` requires the `X-Api-Key` header, or a bearer token when JWT is enabled. **Full interactive documentation is in Swagger UI.**

| Method | Route | Who | Success |
|---|---|---|---|
| `POST` | `/users` | Administrator | 201 + `Location` |
| `GET` | `/users?pageNumber&pageSize&role&sortDirection` | Administrator | 200 paged |
| `GET` | `/users/{userId}` | Administrator | 200 |
| `GET` | `/users/me` | any | 200 |
| `GET` | `/users/me/ships` | any | 200 |
| `GET` | `/users/{userId}/ships` | self or Administrator (else 403) | 200 |
| `PUT` | `/users/{userId}/ships/{shipCode}` | Administrator, idempotent | 201 new / 204 already assigned |
| `DELETE` | `/users/{userId}/ships/{shipCode}` | Administrator, idempotent | 204 |
| `POST` | `/users/{userId}/api-keys` | Administrator | 201 (key shown **once**) |
| `GET` | `/users/{userId}/api-keys` | Administrator | 200 (metadata only) |
| `DELETE` | `/users/{userId}/api-keys/{apiKeyId}` | Administrator | 204 |
| `POST` | `/ships` | Administrator | 201 + `Location` |
| `GET` | `/ships?pageNumber&pageSize&status` | any (non-admins see assigned ships) | 200 paged |
| `GET` | `/ships/{shipCode}` | Administrator or assigned | 200 |
| `PATCH` | `/ships/{shipCode}` (`shipName`, `status`; optional `If-Match`) | Administrator | 200 (412 if the ETag is stale) |
| `GET` | `/ships/{shipCode}/crew?pageNumber&pageSize&sortBy&sortDirection&search` | Administrator or assigned | 200 paged |
| `GET` | `/ships/{shipCode}/financial-reports/detail?period=yyyy-MM` | Administrator or assigned | 200 |
| `GET` | `/ships/{shipCode}/financial-reports/summary?period=yyyy-MM` | Administrator or assigned | 200 |
| `GET` | `/health`, `/health/ready` | anonymous | 200 / 503 |

**Optimistic concurrency:** `GET`, `POST` and `PATCH /ships/{code}` return an `ETag` (the ship's `version`, also shown in list items). Send it back as `If-Match` on `PATCH`. If someone changed the ship in the meantime, the update is rejected with 412 instead of overwriting their change. `If-Match` is optional (omit it, or send `*`, for last-writer-wins).

**Paged envelope:** `{ "items": [...], "pageNumber": 1, "pageSize": 20, "totalCount": 23, "totalPages": 2 }`

**Errors** are [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem documents (`application/problem+json`). They carry a stable `code`, a `traceId` for log correlation and, for validation, every failing field at once:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "code": "VALIDATION_FAILED",
  "errors": {
    "pageSize": ["pageSize must be between 1 and 100."],
    "sortBy": ["sortBy must be one of: rank, crewMemberId, firstName, lastName, age, nationality, signOnDate, status."]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

| Status | When |
|---|---|
| 400 | Invalid input, malformed JSON, **unknown JSON properties** (mass-assignment protection), non-numeric query values |
| 401 | Missing, malformed, unknown, revoked or expired key, or an inactive user. The response is identical in every case. |
| 403 | Authenticated, but the operation needs the Administrator role, or the caller asked for another user's ships |
| 404 | Unknown resource, **or a ship not assigned to the caller** (indistinguishable, which prevents enumeration) |
| 409 | Duplicate ship code or e-mail; `SHIP_INACTIVE` |
| 412 | `If-Match` ETag is stale: the ship changed since it was read (`PRECONDITION_FAILED`) |
| 413 / 415 | Body over 64 KB (enforced by Kestrel) / content type not JSON |
| 429 | Rate limit exceeded (`Retry-After` header) |
| 500 | Unexpected. The response gives a generic message and `traceId`, never a stack trace or SQL text. |
| 503 | Database temporarily unavailable, after retries or while the circuit is open (`DATABASE_UNAVAILABLE`, `Retry-After` header) |

---

## Security

The system is designed around the OWASP API Security Top 10 and the maritime cyber-risk guidance (IMO MSC-FAL.1/Circ.3/Rev.3, BIMCO v5). Crew records are personal data, and owners' accounts are commercially confidential.

| Control | Implementation |
|---|---|
| **SQL injection** (SEC-06) | The API executes **stored procedures only** (`CommandType.StoredProcedure`), with typed and sized parameters. There is no SQL text in C#, not even for health checks. There is **no dynamic SQL** in any procedure. Sort keys are whitelisted in the API **and** in the procedure, and LIKE wildcards are escaped. A test suite of injection payloads proves the schema and row counts are unchanged. |
| **Least privilege** (SEC-05) | The API login is a member of `app_executor`, which has **only `EXECUTE ON SCHEMA::app`**. It has no table permissions of any kind, and ownership chaining does the rest. Tests prove the login gets *permission denied* on `SELECT`, `UPDATE` or `DELETE` against tables, and on internal procedures. |
| **Authentication** (SEC-03) | `X-Api-Key` keys are 256 bits from a CSPRNG, in the format `sm_<prefix>_<secret>`. Only the **SHA-256 hash** is stored. The non-secret prefix identifies keys in logs. Keys have expiry and revocation, and a malformed key is rejected before any database work. Optional **JWT bearer** (issuer, audience, lifetime, signature, 2-minute clock skew) maps `sub` to an **active** user. |
| **Authorization** (SEC-04) | Function level: ASP.NET Core policies (Administrator), with a fallback policy of *authenticated* on every endpoint. Object level (BOLA): enforced **twice**, in the API (`ICurrentUser.CanAccessShip`) **and** inside every procedure via `@RequestedByUserId`. A ship that is not assigned gives the same 404 as a missing one. |
| **Input validation** (SEC-07) | FluentValidation at the edge, repeated in the procedures. Unknown JSON properties are rejected, JSON depth is limited, the body is limited to 64 KB, and only `application/json` is accepted. Ids, audit data and status defaults are server-controlled. |
| **Error exposure** (SEC-08) | One global handler returns curated problem details. Responses contain only what the use case needs (age, not birth date; no internal keys). |
| **Logging and audit** (SEC-09) | Structured JSON logs through source-generated `LoggerMessage`. One line per request records the **route template** (never the query string, since a search term may be a name), status, duration and user id. Security events are logged: failed authentication (key prefix only), access denied, rate limiting. **No PII and no secrets in logs**, and a test asserts it. Data changes are written to an **append-only `AuditLog`** (a trigger blocks `UPDATE` and `DELETE`) in the same transaction as the change. |
| **Abuse protection** (SEC-10) | Fixed-window rate limiter per user and per IP address for anonymous traffic (429 + `Retry-After`). Page and search limits, a 15-second command timeout, and cancellation propagated to SQL. |
| **HTTP hardening** (SEC-11) | No `Server` header. `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, a restrictive CSP, and `Cache-Control: no-store` on every API response. HSTS outside Development. HTTPS redirection can be turned on. CORS is deny by default (no frontend). |
| **Secrets** (SEC-02) | No secrets in source. `.env` is git-ignored and `.env.example` is committed. The API receives its connection string from the environment. `sa` is used only by `db-init`. `gitleaks` runs in CI. |
| **Supply chain** (SEC-12) | Central package management, **lock files with `--locked-mode` restore**, and a single pinned NuGet source. CI fails on vulnerable packages. CodeQL, Trivy and hadolint run in CI, and Dependabot covers NuGet, Docker and Actions. The runtime image is multi-stage, **chiseled** (no shell) and **non-root**. In Compose the container is read-only, with all capabilities dropped and `no-new-privileges`. |
| **Privacy** (SEC-14) | Only the crew fields the brief requires are stored. No passport, medical, contact or bank data. The sample people are fictional. |

---

## Testing

**412 tests, all passing.** Unit tests run in about 0.1 s. Integration tests run in about 10 s after the container starts.

| Suite | Count | What it proves |
|---|---|---|
| `ShipManagement.UnitTests` | 240 | Value objects and validators at every boundary (page size 0/1/100/101, search 100/101 characters, periods such as `2025-13`, `2025-7`, `1999-12`). Services with mocked repositories: normalised parameters, access decisions, NULLs preserved, cache invalidation, key lifetime. SQL error mapping for every number. API-key format, hashing and uniqueness. Exception handler output (no internal details). The current-user ship-access rule. |
| `ShipManagement.IntegrationTests` | 172 | A **real SQL Server 2022** (Testcontainers) built by the **same `deploy.sh`** used by Compose and CI, plus deterministic fixtures with `@AsOfDate` pinned to 2025-06-15. |

Integration test highlights (test names state the rule):
- **Crew status boundaries:**
  - `CrewStatus_Is_Onboard_When_Exactly_30_Days_Past_EOC`
  - `CrewStatus_Is_ReliefDue_When_31_Days_Past_EOC`
  - signing on today versus tomorrow
  - a sign-off date in the future
- **Age:** the day before and on the birthday, and a 29 February birth.
- **Search:** `05 Apr`, `apr 2025`, Greek script, wildcards treated literally, injection payloads.
- **Sorting and paging:** every sort key in both directions, rank seniority, paging through every page returns each crew member exactly once, and a page beyond the end.
- **Finance:**
  - the April–March YTD crossing the calendar year, and the first month of a fiscal year;
  - 300 + 0 + 700 = 1000, and duplicate budget lines summed;
  - zero versus null, and all-zero accounts excluded;
  - parents equal to the sum of descendants at 3 levels;
  - Summary tying to Detail, and Grand Total equal to the root.
- **Concurrency and resilience:**
  - two writers with the same ETag: the second gets 412 and its change is not applied (API and procedure level);
  - retries apply only to reads and idempotent writes, and stop at the configured count;
  - the circuit breaker opens after repeated outages, fails fast with 503 + `Retry-After`, and closes again (with a fake clock);
  - business errors never trip it;
  - a database that refuses logins returns 503, never 500.
- **Integrity:**
  - posting to a parent, negative amounts and a mid-month period are all rejected by constraints;
  - audit rows are immutable;
  - deployment can be re-run.
- **Security:**
  - the least-privilege login cannot read tables;
  - identical 401s for bad, revoked, expired and inactive keys, and 403 for non-admin writes;
  - BOLA returns 404; issued keys work at once and revoked keys fail at once;
  - rate limiting, security headers, Brotli compression;
  - logs free of names and keys;
  - JWT (valid, unknown user, wrong signature, expired).

**Coverage** (unit and integration merged; compiler-generated source excluded):

| Project | Line | Branch |
|---|---|---|
| ShipManagement.Api | 99.5% | 94.9% |
| ShipManagement.Application | 100% | 100% |
| ShipManagement.Domain | 100% | 100% |
| ShipManagement.Infrastructure | 100% | 95.8% |

To reproduce locally:

```bash
dotnet test ShipManagement.sln --settings coverage.runsettings --collect "XPlat Code Coverage" --results-directory TestResults
python3 scripts/coverage_gate.py TestResults 90
```

CI fails if any project drops below 90% on lines or branches. The only uncovered lines are Kestrel-only settings (the 64 KB body limit and removing the `Server` header). The in-memory test server doesn't run them; they are verified against the container.

The owner-facing sample data is checked separately by `database/tests/verify_sample_data.sql`, which runs on every deployment.

---

## Operations

**Configuration.** Environment variables use `__` for nesting, e.g. `Database__ConnectionString`.

| Key | Default | Purpose |
|---|---|---|
| `Database:ConnectionString` | — (required; start-up fails without it) | Least-privilege login |
| `Database:CommandTimeoutSeconds` | 15 | Per-command timeout |
| `Database:Resilience:RetryCount` / `RetryBaseDelayMilliseconds` | 2 / 200 | Retries of reads and idempotent writes on transient errors |
| `Database:Resilience:FailureRatio` / `MinimumThroughput` / `SamplingDurationSeconds` / `BreakDurationSeconds` | 0.5 / 10 / 30 / 15 | Circuit breaker |
| `Auth:CacheSeconds` | 30 | Security-context cache (0 disables it) |
| `Auth:Jwt:Enabled` / `Authority` / `Issuer` / `Audience` | off | External identity provider (e.g. Microsoft Entra ID) |
| `RateLimiting:PermitLimitPerUser` / `PermitLimitAnonymous` / `WindowSeconds` | 300 / 60 / 60 | Abuse protection |
| `Swagger:Enabled` | false (true in Development and in the local Compose file) | Swagger UI |
| `Security:RequireHttps` | false | Enable when TLS terminates at the app rather than at a proxy |

- **Health:** `/health` is liveness and has no dependencies. `/health/ready` is readiness: it executes `app.usp_Health_Ping` as the API login. Neither reveals versions or infrastructure details.
- **Logs:** JSON to stdout, ready for any log collector. Correlate a user report with the logs using the problem document's `traceId`.
- **Backup and recovery (ransomware resilience):**
  - Azure SQL: point-in-time restore plus long-term retention.
  - SQL Server or Express: scheduled full and log backups copied off-host, with an immutable copy and regular restore tests.
  - The database can be rebuilt from this repository (`deploy.sh` is idempotent) plus the latest backup.
  - Suggested production targets: **RPO ≤ 15 minutes, RTO ≤ 4 hours**.
- **Incident handling:** the audit log and security-event logs give who, what and when. Keep incident records, as the IMO guidelines require. For **US-flagged vessels**, cyber incidents may be reportable to the US Coast Guard National Response Center (33 CFR 101.600).

---

## Decisions register

These rulings fill gaps in the brief. **D-01 – D-20** were made before implementation; **D-21 – D-28** were made during planning, and **D-29 – D-31** during the concurrency and resilience review.

| ID | Decision |
|---|---|
| D-01 | Crew status is evaluated in the order Signed Off → Planned → Relief Due (more than 30 days after end of contract) → Onboard. 0–30 days after end of contract counts as **Onboard**, and signing on today counts as Onboard. |
| D-02 | "Today" is the **UTC date**. Procedures accept `@AsOfDate` for tests; the API does not expose it. |
| D-03 | "One person per rank" is **not** a constraint, because handovers overlap. |
| D-04 | An inactive ship returns `409 SHIP_INACTIVE` for crew lists and reports. A ship that is not found or not assigned returns the same **404**. Ship lists and "my ships" show inactive ships with their status. |
| D-05 | Several budget or transaction rows for the same ship, account and period are **summed**. |
| D-06 | Stored amounts are `NOT NULL ≥ 0`. A report cell is `null` when there is no data and `0` when rows sum to zero. Variance is `null` only when both sides are `null`. |
| D-07 | Show an account row if any of the four amounts (period and YTD, actual and budget) is non-null and non-zero. |
| D-08 | The COA has unlimited depth. Parents aggregate **all** descendants. Postings to parent accounts are impossible. |
| D-09 *(amended)* | **Detail** is every qualifying account (parents and children). **Summary** is the qualifying **parent (summary) accounts**, following the brief's own wording "parent (summary) or child (detail)". Both end with a grand total, and the summary must tie to the detail. |
| D-10 | The API uses periods as `yyyy-MM`. The database stores them as a `DATE` on the first of the month. |
| D-11 | Fiscal years live in a reference table with consistency checks: 0112, 0403, 0706, 1009. |
| D-12 | Ship code `^[A-Z0-9]{3,10}$` (input is upper-cased). Crew ID `^[A-Z0-9]{3,20}$`. Account number `VARCHAR(20)`. |
| D-13 | At least 20 crew for **every** sample ship. |
| D-14 | Search is one case-insensitive *contains* term across every column except status, including the `dd MMM yyyy` label. Wildcards are literal. Maximum 100 characters. |
| D-15 | Sorting uses whitelisted keys; `rank` means seniority. The crew ID is always the final tie-breaker. |
| D-16 | `pageNumber ≥ 1`; `pageSize` 1–100, default 20. A page beyond the end is empty with 200. The response includes `totalCount` and `totalPages`. |
| D-17 | Amounts are `DECIMAL(19,2)` in a single reporting currency. Multi-currency is not designed out. |
| D-18 | Roles are Administrator, FleetManager, Superintendent, CrewingOfficer, Accountant and OwnerRepresentative. Only administrators manage users, ships, assignments and keys, and they see all ships. |
| D-19 | Age is counted in completed years at the as-of date. |
| D-20 | There are no hard deletes of crew or finance data. Ships are deactivated, not deleted. Audit rows are immutable. |
| D-21 | Generated sample crew dates are relative to the seed date. The brief's own records keep their exact dates, and tests use fixed fixtures. |
| D-22 | A parent account must be of type `P`, enforced declaratively with a composite self-FK. |
| D-23 | `PATCH /ships/{code}` changes only the name and status. The fiscal year is immutable, because changing it would restate history. |
| D-24 | API keys are issued by administrators and returned once. Only the hash is stored. Local development keys are **generated at random on first deployment** (`09_dev_api_keys.sql`) and printed once; none is stored in the repository. They expire after 180 days and can be rotated with `ROTATE_DEV_KEYS=1`. |
| D-25 | The resolved caller context (user, role, ships) is cached in-process for 30 s and invalidated immediately on assign, unassign and revoke. On other instances a change takes effect within 30 s. |
| D-26 | Swagger is controlled by configuration, not by environment. It is on for local Compose, and API calls from it still need a key. |
| D-27 | Internal working notes are kept out of the repository. |
| D-28 | Controllers (`[ApiController]`) rather than Minimal APIs, for attribute routing, rich OpenAPI metadata and a familiar layout. |
| D-29 | **Caching is limited to the caller's security context** (D-25). Crew lists and reports are **not** cached, for four reasons. Crew status changes with "today" and finance data can be posted late, so a stale answer is a wrong answer. Responses are per user and per authorization. They carry personal and financial data sent with `no-store` (SEC-11). And the procedures take only milliseconds. The scaling path is read replicas (Azure SQL read scale-out), or a short-lived output cache for closed periods, if load ever requires it. |
| D-30 | **Optimistic concurrency on ship updates:** the ETag is the `rowversion`. `If-Match` is checked inside `usp_Ship_Update` under an update lock, and a stale ETag gives 412. `If-Match` is optional, so tools like Swagger still work. |
| D-31 | **Database resilience with Polly v8:** transient-error retries for reads and idempotent writes only (never for creates or updates, which could be applied twice), plus a per-instance circuit breaker that fails fast with 503 + `Retry-After` during an outage. Business errors never trip the breaker. |

## Notes on the brief

The brief's examples are illustrative, and a few contradict its rules. This implementation follows the **rules**:

- The example crew output is only true for a 2025 "today". For example, CREW001's end of contract is 2025-07-05, so the crew member is *Relief Due* after 2025-08-04. CREW002 signs on on 04 Apr in the data but 05 Apr in the example. Tests pin `@AsOfDate` instead of copying the example.
- In the example financial output, the parent 7000000 shows a blank budget while its child has 3300. The YTD variance shows 0 where 9600 − 9900 = −300, and one row is misaligned. The rules (parent = Σ children, variance = actual − budget) are implemented instead.
- Account `7135000` is used in the data but missing from the sample COA. It is added under `7100000`, together with `7120000 AWARDS`.
- The duplicate budget rows (0 and 1000 for SHIP01/7135000/2025-01) are summed (D-05).
- "Emsire stored procedures are safe from SQL injection" is read as "**Ensure**".

## Production hardening and future work

- An API gateway or WAF in front, TLS end to end (`Encrypt=True; TrustServerCertificate=False`), and a Microsoft Entra managed identity for the database instead of a SQL login.
- A distributed cache (e.g. Redis) or short-lived tokens, if immediate revocation is required across many instances.
- Centralised SIEM integration and alerting on security events, plus automated key rotation.
- Always Encrypted or Dynamic Data Masking for birth dates. Retention and anonymisation jobs for signed-off seafarers.
- Crew and finance write APIs (sign on/off, budget import) with the same audit and constraint model.
- Multi-currency reporting, and variance commentary for owner reports.
- Image digest pinning and a signed SBOM in the release pipeline.
