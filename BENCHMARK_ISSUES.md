# Benchmark issues

These four issues are **not implemented** in the baseline. They are the work items for a public, fair comparison of coding agents (Codex and Claude Code).

How to use them:

- Give the agent this repository and **exactly one** issue below, copied in full.
- Do not add hidden requirements, and do not ask the agent to implement more than one issue.
- The agent must keep every behavior that is not named in the issue. All existing tests must still pass, and new tests must cover the acceptance criteria.
- Do not change ticket-key allocation (`{projectKey}-{number}`, numbers never reused), the SQLite + EF Core setup, the `ProblemDetails` error style, or unrelated endpoints.
- Differences in private structure are fine. The HTTP contract, status codes, and error shape in the issue are not optional.

Each issue is about 30–60 minutes of work for a developer who already knows this codebase.

---

## Issue 1 — Comments on tickets

### Background

Tickets have no discussion. Add comments as a child resource. Do not add comment editing, reactions, mentions, or a comment count on the ticket payload.

### Data model

Add a `Comment` entity and an EF Core migration:

| Field | Rules |
| --- | --- |
| `Id` | int, identity |
| `TicketId` | required FK to ticket. Deleting a ticket deletes its comments (cascade) so ticket delete still returns `204` |
| `AuthorId` | required FK to user, `Restrict` |
| `Body` | required, trimmed, 1–5000 characters |
| `CreatedAt` | UTC `DateTimeOffset`, set by the server |

Use the same `DateTimeOffset` storage approach as the other entities. Cascade comment rows when a ticket is deleted so `DELETE /api/projects/{projectId}/tickets/{ticketId}` still returns `204` and removes that ticket's comments. Do not cascade when a user is removed (users cannot be deleted through the API).

### API

| Method | Path | Success |
| --- | --- | --- |
| `GET` | `/api/projects/{projectId}/tickets/{ticketId}/comments` | `200` array, oldest first (`createdAt`, then `id`) |
| `POST` | `/api/projects/{projectId}/tickets/{ticketId}/comments` | `201 Created` with `Location` |
| `DELETE` | `/api/projects/{projectId}/tickets/{ticketId}/comments/{commentId}` | `204` |

`POST` body:

```json
{ "body": "Looks like a null check is missing.", "authorId": 1 }
```

Response item:

```json
{
  "id": 10,
  "ticketId": 4,
  "body": "Looks like a null check is missing.",
  "author": { "id": 1, "name": "Ada Lovelace", "email": "ada@example.com" },
  "createdAt": "2026-01-15T12:00:00.0000000+00:00"
}
```

Do not embed comments inside `TicketResponse`. `GET` returns a JSON array, not a paged object. An empty list is `[]`.

### Validation and errors

Use the existing `application/problem+json` shape, including `traceId`.

| Situation | Status |
| --- | --- |
| Missing or whitespace `body`, body longer than 5000 after trim, missing or non-positive `authorId` | `400` validation problem (`errors`) |
| `authorId` does not match a user | `400` problem, detail mentions `Author` |
| Project missing, ticket missing, or ticket not in that project | `404` |
| Comment id missing or not on that ticket | `404` |
| Blank body stored | never — trim first; whitespace-only is `400` |

Creating a comment does not change the ticket's `updatedAt` or `status`.

### Tests

Add integration tests (`WebApplicationFactory`) that:

1. Create two comments and list them oldest first, with the author object filled in.
2. Return `201` and a `Location` that `GET`s the collection containing the new comment (or the location path ends with the comment URL; the collection `GET` must include it).
3. Reject an empty body and an unknown author.
4. Return `404` when the ticket belongs to another project.
5. Delete a comment (`204`), then show it is gone, and return `404` for a second delete.
6. Deleting the ticket removes its comments (a later comment list or a direct delete of the old comment id returns `404`).

All existing tests must pass.

### Out of scope

Editing a comment, paging, markdown rendering, notifications, and showing comments on `GET` ticket.

### Acceptance criteria

- [ ] Migration adds `Comment` and ticket delete still returns `204`.
- [ ] List, create, and delete match the contract above.
- [ ] Validation and not-found errors are `ProblemDetails`.
- [ ] Swagger documents the three operations.
- [ ] New tests cover the cases above and `dotnet test` passes.

---

## Issue 2 — Status workflow rules

### Background

The baseline accepts any `status` on create and any change on update. `ToDo` can jump straight to `Done`. Replace that with a fixed workflow. Do not add a configurable workflow, per-project rules, or comments.

### Allowed transitions

Check the graph **only when `status` changes**. A `PUT` that keeps the same status (and changes the title, for example) succeeds.

| From | Allowed destinations |
| --- | --- |
| `ToDo` | `InProgress` |
| `InProgress` | `ToDo`, `InReview` |
| `InReview` | `InProgress`, `Done` |
| `Done` | `InProgress` |

Everything else is rejected, including `ToDo` → `Done`, `ToDo` → `InReview`, `InProgress` → `Done`, `Done` → `ToDo`, `Done` → `InReview`, and `InReview` → `ToDo`.

### Create versus update

- `POST` may omit `status` (default `ToDo`) or send `ToDo`.
- `POST` with any other status returns `400` and does not create a ticket. Detail or validation errors must say that a new ticket starts in `ToDo`.
- `PUT` with a disallowed change returns `409 Conflict` (not `400`), does not modify the ticket, and does not change `updatedAt`.
- The `409` problem `detail` must include the current status, the requested status, and the allowed destinations. Example: `Cannot move TT-1 from ToDo to Done. Allowed: InProgress.`
- `title` stays `Conflict`, `status` is `409`, content type is `application/problem+json`, and `traceId` is present.

### Tests

Add unit or integration tests that:

1. Allow each edge in the table (six successful updates). Same-status updates still succeed.
2. Reject `ToDo` → `Done`, `ToDo` → `InReview`, `InProgress` → `Done`, and `Done` → `ToDo` with `409`, and assert the ticket is unchanged.
3. Reject `POST` with `status: Done` with `400` and assert no ticket was created.
4. Still allow `POST` that omits `status` (`ToDo`, key assigned as today).

Existing tests only create tickets as `ToDo` and only move `ToDo` → `InProgress`. They must keep passing. Do not weaken them.

### Out of scope

Required fields per status, automatic assignee changes, and recording the transition in an activity log (that is issue 4).

### Acceptance criteria

- [ ] Only the transitions in the table succeed.
- [ ] Illegal updates are `409` with from, to, and allowed statuses, and they do not persist.
- [ ] Create only allows `ToDo`.
- [ ] Tests cover every allowed edge and the rejected cases named above.
- [ ] `dotnet test` passes, including the original suite.

---

## Issue 3 — Ticket search and sort

### Background

`GET /api/projects/{projectId}/tickets` filters by `status` and `assigneeId` and always orders by ticket number ascending. Add text search and sort **on that same endpoint**. Do not add a new route.

### Query parameters

| Parameter | Values | Default |
| --- | --- | --- |
| `q` | substring matched against `title` or `description` | omitted means no text filter |
| `sort` | `key`, `createdAt`, `updatedAt`, `priority` | `key` |
| `direction` | `asc`, `desc` | `asc` |

Rules:

- `q`, `status`, and `assigneeId` combine with AND.
- `q` is trimmed. Empty or whitespace `q` is ignored. Maximum length 200; longer values are `400` validation problems.
- Match is case-insensitive. `Login` matches a title `login button`. A match in either `title` or `description` is enough. Null descriptions do not match.
- `sort=key` orders by ticket **number**, not by the string `TT-10` vs `TT-2`, so `TT-2` comes before `TT-10` when ascending.
- `sort=priority` uses `Lowest < Low < Medium < High < Highest`.
- `createdAt` and `updatedAt` use the stored timestamps.
- When the sort field ties, break the tie by ticket number ascending so paging is stable.
- Invalid `sort` or `direction` is `400` with a validation `errors` entry. Do not silently fall back to the default.
- Omitting the new parameters preserves today's behavior: number ascending, existing status and assignee filters, same page shape.
- Unknown project is still `404`. An unknown `assigneeId` is still an empty page.

### Tests

Add integration tests that:

1. Find a ticket by a case-insensitive title substring and by a description substring, and exclude non-matches.
2. Combine `q` with `status` so a text match in the wrong status is excluded.
3. Sort `priority` descending (`Highest` first) and `key` ascending with a number greater than 9 so `...-2` precedes `...-10`.
4. Sort `updatedAt` descending after an update, with the tie-break covered or asserted.
5. Return `400` for `sort=title` and for `direction=sideways`.
6. Leave a no-parameter list ordered by ticket number, so existing paging tests still pass.

### Out of scope

Full-text indexes, highlighting, search across projects, and fuzzy matching.

### Acceptance criteria

- [ ] `q`, `sort`, and `direction` behave as specified and appear in Swagger.
- [ ] Defaults match the current list order and filters.
- [ ] Invalid sort or direction is a validation problem.
- [ ] New tests cover the cases above and `dotnet test` passes.

---

## Issue 4 — Activity history of ticket changes

### Background

Nothing records who changed a ticket or what changed. Add an append-only activity log for creates and updates. Do not log reads or deletes, and do not add a global audit screen.

### Data model

Add `TicketActivity` and a migration:

| Field | Rules |
| --- | --- |
| `Id` | int, identity |
| `TicketId` | required FK, cascade delete with the ticket |
| `ActorId` | optional FK to user, `Restrict` |
| `Type` | `Created` or `Updated` |
| `Field` | null for `Created`; for `Updated`, one of `title`, `description`, `type`, `priority`, `status`, `assigneeId` |
| `OldValue` | null for `Created`; previous value for `Updated` |
| `NewValue` | null for `Created`; new value for `Updated` |
| `CreatedAt` | UTC timestamp set by the server |

Store enum values as their names (`InProgress`, `Bug`, `High`). Store `assigneeId` as the decimal integer string (`1`) or JSON `null` in the database when the assignee is null (the response uses JSON `null`, not `""`). Store text as the trimmed value that was saved. One `Updated` row per changed field, not one row for the whole request.

### When to write

- `POST` ticket writes a single `Created` row. `actorId` is the reporter. `field`, `oldValue`, and `newValue` are null. This is part of the same transaction as the insert.
- `PUT` compares title, description, type, priority, status, and assignee. Write one `Updated` row for each value that actually changed.
- If none of those fields changed, write nothing and **do not** modify `updatedAt`.
- If at least one field changed, set `updatedAt` to the current time (as today) and use that same timestamp on the new activity rows.
- Add an optional `actorId` to `UpdateTicketRequest`. If it is omitted, store `actorId` as null. If it is present and the user does not exist, return `400`, mention `Actor`, and persist nothing.
- Existing clients that omit `actorId` keep working.
- Delete does not write an activity row. Deleting the ticket deletes its activity rows.

### API

`GET /api/projects/{projectId}/tickets/{ticketId}/activity`

- `200` JSON array, oldest first (`createdAt`, then `id`).
- `404` if the project or ticket does not exist, or the ticket is in another project.
- Not paged.

```json
[
  {
    "id": 1,
    "ticketId": 4,
    "type": "Created",
    "field": null,
    "oldValue": null,
    "newValue": null,
    "actor": { "id": 2, "name": "Grace Hopper", "email": "grace@example.com" },
    "createdAt": "2026-01-15T12:00:00.0000000+00:00"
  },
  {
    "id": 2,
    "ticketId": 4,
    "type": "Updated",
    "field": "status",
    "oldValue": "ToDo",
    "newValue": "InProgress",
    "actor": null,
    "createdAt": "2026-01-15T13:00:00.0000000+00:00"
  }
]
```

If issue 2's workflow is not present, do not add it. Record whatever status change the baseline allows.

### Tests

Add integration tests that:

1. Creating a ticket writes one `Created` activity whose actor is the reporter.
2. Updating status and title writes two `Updated` rows with the right old and new values, and omits unchanged fields.
3. A `PUT` that repeats the current values adds no activity and leaves `updatedAt` unchanged.
4. An unknown `actorId` returns `400` and does not append activity or change the ticket.
5. `GET` activity for a ticket that lives in another project returns `404`.
6. Deleting the ticket makes `GET` activity return `404`.

### Out of scope

Activity for project edits, comment edits, HTTP request logging, and reverting a change.

### Acceptance criteria

- [ ] Creates and per-field updates are stored and listed oldest first.
- [ ] No-op updates do not write a row and do not bump `updatedAt`.
- [ ] Optional `actorId` is validated.
- [ ] Swagger documents `GET .../activity` and the new request field.
- [ ] New tests cover the cases above and `dotnet test` passes.
