# ADR 0002 — CQRS with MediatR

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-02-23 |
| **Deciders** | Engineering team |

## Context

The application needs to handle a mix of write operations with complex business rules (creating events, sending invitations) and read operations that may need different data shapes (event lists with RSVP summaries, participant views). Placing all logic in controllers or a single service layer leads to bloated classes, difficult testing, and tight coupling between read and write concerns.

## Decision

We apply **Command Query Responsibility Segregation (CQRS)** at the application layer using **MediatR** as the in-process mediator.

- All state-changing operations are expressed as **Commands** (`IRequest<T>` returning a result or `Unit`).
- All read operations are expressed as **Queries** (`IRequest<T>` returning a DTO or collection).
- Each command/query has a single, focused **Handler** class.
- Cross-cutting concerns (validation, logging, performance) are implemented as **Pipeline Behaviours** registered once.

### Pipeline Behaviours (ordered)

1. `LoggingBehaviour` — logs command name and duration
2. `ValidationBehaviour` — runs FluentValidation; short-circuits with `ValidationException` on failure
3. _(future)_ `TransactionBehaviour` — wraps commands in a `IUnitOfWork` transaction

## Alternatives Considered

| Option | Reason not chosen |
|---|---|
| Fat service layer (`IEventService`) | Single class grows unbounded; hard to test in isolation |
| Controller-based logic | Violates single responsibility; no reuse across transports |
| Full CQRS with separate read DB | Overkill for v1 scale; read/write from same SQL DB is acceptable at this size |
| Custom mediator | MediatR is the de-facto standard; no benefit to reinventing it |

## Consequences

### Positive
- Each handler has a single responsibility and is trivially unit-testable in isolation.
- New features are added by adding new handlers — existing code is not modified (Open/Closed Principle).
- Pipeline behaviours provide a single place for cross-cutting logic without decorator boilerplate.
- Commands and Queries act as an explicit, self-documenting API surface for the application layer.

### Negative / Trade-offs
- Slight indirection — a developer must navigate from controller → MediatR `Send()` → handler rather than a direct call.
- More files per feature (command, query, handler, validator, DTO) — mitigated by consistent folder structure.
- `MediatR` introduces a NuGet dependency; however it is stable, widely used, and unlikely to be a migration risk.
