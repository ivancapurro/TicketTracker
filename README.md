# Ticket Tracker

> **Introducción.** API REST de seguimiento de tickets, al estilo de Jira, pensada como punto de partida compartido para un benchmark público y justo entre agentes de programación (Codex y Claude Code). Esta línea base deja fuera, a propósito, cuatro funcionalidades. Cada una está descrita como un issue idéntico en [`BENCHMARK_ISSUES.md`](BENCHMARK_ISSUES.md), con criterios de aceptación, para que cualquier agente reciba el mismo enunciado.

## What this is

Ticket Tracker is a small ASP.NET Core Web API (.NET 10, the current LTS) that manages projects and tickets. It is the shared starting point for a public benchmark: two coding agents are given the same repository and the same issue, and their results are compared.

The baseline is intentionally conventional and small:

- Controller-based REST API
- Entity Framework Core with SQLite (no database server to install)
- Migrations and seed data applied on startup
- Request validation and [RFC 9457](https://datatracker.ietf.org/doc/html/rfc9457) `ProblemDetails` errors
- Swagger UI / OpenAPI
- xUnit unit tests and `WebApplicationFactory` integration tests

There is no authentication. Users are reference data, seeded at startup, and are not created through the API.

## Run

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (any 10.0 patch; `global.json` rolls forward within the 10.0 feature band).

From the repository root:

```bash
dotnet build
dotnet test
dotnet run --project src/TicketTracker.Api
```

The API listens on `http://localhost:5080`. Swagger UI is at <http://localhost:5080/swagger> (the site root redirects there outside Production). OpenAPI JSON is at <http://localhost:5080/swagger/v1/swagger.json>.

The first run creates `src/TicketTracker.Api/Data/tickettracker.db`, applies migrations, and seeds users plus two sample projects. Delete that file to start over.

`dotnet ef` is a local tool. Restore it with `dotnet tool restore` if you need to add a migration:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/TicketTracker.Api
```

## Layout

```
src/TicketTracker.Api/          ASP.NET Core host
  Controllers/                  HTTP adapters
  Services/                     Project, ticket, and user use cases
  Data/                         EF Core context, migrations, seed
  Domain/                       Entities, enums, key rules
  Contracts/                    Request and response models
  Validation/                   Data-annotation attributes
  Errors/                       ProblemDetails exception handling
tests/TicketTracker.Api.Tests/  Unit tests and WebApplicationFactory tests
BENCHMARK_ISSUES.md             The four issues that are deliberately not implemented
```

Controllers stay thin. Services use `TicketTrackerDbContext` directly. There is no repository layer and no extra class libraries.

## Domain

| Entity | Fields |
| --- | --- |
| Project | `key` (for example `TT`), `name`, `description`, `createdAt` |
| User | `name`, `email` (seeded, no login) |
| Ticket | `key` (for example `TT-1`), `title`, `description`, `type`, `priority`, `status`, `assignee`, `reporter`, `createdAt`, `updatedAt` |

Ticket keys are `{projectKey}-{number}`. The number starts at 1 for each project and is never reused, including after a ticket is deleted.

Enums are JSON strings (integer values are rejected):

| Field | Values |
| --- | --- |
| `type` | `Bug`, `Task`, `Story` |
| `priority` | `Lowest`, `Low`, `Medium`, `High`, `Highest` |
| `status` | `ToDo`, `InProgress`, `InReview`, `Done` |

The baseline does **not** restrict status changes. Any status can be set on create or update. Allowed transitions are benchmark issue 2.

A project key is 2–10 uppercase letters (`A`–`Z`), stored without surrounding spaces (`" TT "` is saved as `TT`). It cannot be changed after create. Names and titles are trimmed. A blank description is stored as `null`.

## API

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/users` | List seeded users |
| `GET` | `/api/projects` | List projects (ordered by key) |
| `POST` | `/api/projects` | Create a project |
| `GET` | `/api/projects/{id}` | Get a project |
| `PUT` | `/api/projects/{id}` | Update name and description |
| `DELETE` | `/api/projects/{id}` | Delete a project that has no tickets |
| `GET` | `/api/projects/{projectId}/tickets` | List tickets, with filters and paging |
| `POST` | `/api/projects/{projectId}/tickets` | Create a ticket |
| `GET` | `/api/projects/{projectId}/tickets/{ticketId}` | Get a ticket |
| `PUT` | `/api/projects/{projectId}/tickets/{ticketId}` | Replace mutable ticket fields |
| `DELETE` | `/api/projects/{projectId}/tickets/{ticketId}` | Delete a ticket |

Ticket list query parameters:

| Parameter | Default | Notes |
| --- | --- | --- |
| `status` | none | Exact status match |
| `assigneeId` | none | Exact assignee. Unknown ids return an empty page, not an error |
| `page` | `1` | 1-based, max 10000 |
| `pageSize` | `20` | 1–100 |

The list is ordered by ticket number ascending. The body is:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

Create project:

```bash
curl -s -X POST http://localhost:5080/api/projects \
  -H 'Content-Type: application/json' \
  -d '{"key":"DEMO","name":"Demo","description":"Example project"}'
```

Create ticket (`status` defaults to `ToDo` when omitted):

```bash
curl -s -X POST http://localhost:5080/api/projects/1/tickets \
  -H 'Content-Type: application/json' \
  -d '{"title":"Export CSV","type":"Task","priority":"Medium","reporterId":1}'
```

List tickets:

```bash
curl -s 'http://localhost:5080/api/projects/1/tickets?status=ToDo&assigneeId=1&page=1&pageSize=20'
```

Successful creates return `201 Created` with a `Location` header. Deletes return `204 No Content`.

### Errors

Validation failures return `400` with `application/problem+json` and an `errors` object. Missing projects or tickets return `404`. A duplicate project key, or deleting a project that still has tickets, returns `409`. A reporter or assignee id that does not exist returns `400`. Unexpected failures return `500` with a generic detail and no stack trace. Every problem response includes `traceId`.

### Seed data

Users:

| Name | Email |
| --- | --- |
| Ada Lovelace | ada@example.com |
| Alan Turing | alan@example.com |
| Grace Hopper | grace@example.com |

Sample projects (Development only; tests seed users and create their own projects):

| Key | Name | Tickets |
| --- | --- | --- |
| `TT` | Ticket Tracker | `TT-1` bug, high, in progress, assignee Ada, reporter Grace. `TT-2` story, medium, to do, unassigned, reporter Alan |
| `WEB` | Website | `WEB-1` task, low, done, assignee Grace, reporter Ada |

Ids are assigned by the database. Look up users with `GET /api/users` rather than assuming numeric ids.

## Benchmark issues

The following are specified in [`BENCHMARK_ISSUES.md`](BENCHMARK_ISSUES.md) and are **not** implemented here. Give an agent exactly one issue, as written, plus this repository. Do not add extra scope.

1. **Comments on tickets** — list, create, and delete comments.
2. **Status workflow** — allowed transitions only (for example `ToDo` cannot move directly to `Done`), with tests.
3. **Text search and sort** — `q`, `sort`, and `direction` on the ticket list.
4. **Activity history** — an audit trail of ticket creates and field changes.

Each issue is sized at roughly 30–60 minutes of human work and includes acceptance criteria so both agents receive the same prompt.

## License

[MIT](LICENSE)
