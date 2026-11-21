# Agent documentation

Guidelines for this repository. English is the language of code, APIs, errors, logs, and identifiers.

## Product

Dante is a personal finance tracker for Brazil. The server is an ASP.NET Core application. The client will be a separate application and is out of scope until requested.

PostgreSQL runs through Docker Compose. The API reads `ConnectionStrings__Dante` and `Jwt__SigningKey` from the environment. Those values stay out of source. `.env` is local and gitignored. `.env.example` lists the names. The process refuses to start when either value is missing.

Passwords are stored as PBKDF2 hashes. Authentication uses a signed JWT.

Users have a username, a password hash, and a creation time. Accounts belong to one user and one real bank. Deleting a user deletes that user's accounts. Deleting an account does not delete the user. A bank that still has accounts cannot be deleted.

Banks come from `https://brasilapi.com.br/api/banks/v1`. ISPB is the stable key. On startup, new banks are inserted and changed names, COMPE codes, and logos are updated. Banks that disappear from the source stay in the database.

## Projects

| Project | Role |
|---|---|
| `Dante.Server` | Composition root and HTTP controllers |
| `Dante.Users`, `Dante.Accounts`, `Dante.Banks` | One project per domain |
| `Dante.Infrastructure` | Database, external services, hashing, JWT, exception translation, and telemetry |

A new domain gets its own project with the same split. Do not put domain rules back into `Dante.Server`.

## Layers

Domain code holds entities, invariants, domain exceptions, and the repository interfaces. It does not reference Entity Framework, HTTP, the database, or OpenTelemetry.

Application services apply use cases. They depend on interfaces, orchestrate repositories, and call domain rules. Register them with dependency injection from the domain project (`AddUsers`, `AddAccounts`, `AddBanks`).

Repositories are the only place that talks to persistence or to an external query source. Database access and the BrasilAPI client are both repositories. Their implementations live in `Dante.Infrastructure` and are registered from `AddDanteInfrastructure`.

Controllers are thin. Use `[ApiController]` and let `AddControllers` / `MapControllers` discover them. A controller calls a service and returns the result. It does not apply business rules or query the database.

## Exceptions

Define specific exception types in the domain that owns the rule. Infrastructure translates persistence failures into those exceptions, such as a unique-index violation becoming `UsernameAlreadyInUseException`. `DomainExceptionHandler` maps domain exceptions to HTTP status codes. Do not catch domain exceptions inside controllers to build error responses.

## Logging and OpenTelemetry

Use OpenTelemetry for traces, metrics, and logs. Create one provider per process through dependency injection at startup, and let the host dispose it on shutdown.

Context propagation uses W3C Trace Context. An incoming request restores the caller's span from the `traceparent` header, and that request becomes one span in the same trace. An outgoing `HttpClient` call injects the active span, so the call is a child of the operation that started it. Do not pass trace ids or span ids by hand. Baggage is only for extra values that must ride along with the trace. Span links are for a relationship that is not a parent and child.

Prefer the ASP.NET Core and `HttpClient` instrumentations for HTTP spans. Add a custom `Activity` only for work that has no incoming request, such as the bank catalog sync. Reuse one `ActivitySource` named `Dante`. Check `IsAllDataRequested` before setting tags, set them with `SetTag`, and finish the activity with `using`. Register that source with `AddSource`.

Attributes that do not change during the process, such as the service name and the environment, belong on the resource. `OTEL_SERVICE_NAME` overrides the default service name `Dante`.

Logs emitted while a span is active include its trace id and span id. The console logger includes those scopes. Do not log passwords, tokens, connection strings, or signing keys, and do not put those values on span attributes.

Entity Framework writes the command text only in Development, and only for a command that succeeds. Parameter values are not logged. A failed command is always recorded: Entity Framework logs it at error, including the command text with parameter placeholders, and Dante adds a child span of the active operation. That span status is Error. Its attributes are `db.system.name`, `db.namespace`, `db.operation.name`, `db.response.status_code` when PostgreSQL returned a SQL state, and `error.type`. The exception is recorded on the span. The matching log carries those same fields and the exception, and it does not include the command text, parameter values, or the connection string.

A domain exception is logged at warning with its status code and exception type, and the active span is tagged with `exception.type`. An unexpected exception is logged at error, the span status is set to Error, and the same tag is applied. Those fields are how a failure is found and tied back to its trace.

Export traces, metrics, and logs through OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Credentials for that endpoint stay in `OTEL_EXPORTER_OTLP_HEADERS`. Telemetry setup lives in `Dante.Infrastructure`.

## Current HTTP surface

| Method | Route |
|---|---|
| `POST` | `/api/auth/register` |
| `POST` | `/api/auth/login` |
| `GET` | `/api/banks` |
| `GET` | `/api/accounts` |
| `POST` | `/api/accounts` |
| `DELETE` | `/api/accounts/{id}` |

Bank and account routes require the JWT. Account queries and deletes are limited to the user in the token.
