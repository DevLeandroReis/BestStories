# BestStories API

A RESTful API built with ASP.NET Core that returns the best *n* Hacker News stories, ordered by
score descending — implemented for the Santander developer coding test.

```
GET /stories/{n}
```

---

## Table of Contents

- [Getting Started](#getting-started)
- [The Endpoint](#the-endpoint)
- [Serving Load Without Overloading Hacker News](#serving-load-without-overloading-hacker-news)
- [Architecture](#architecture)
- [Design Decisions](#design-decisions)
- [Configuration](#configuration)
- [Testing](#testing)
- [Assumptions](#assumptions)
- [Given More Time](#given-more-time)

---

## Getting Started

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Run the API:

```bash
dotnet run --project src/BestStories.API/BestStories.API.csproj
```

Run the full test suite:

```bash
dotnet test
```

The API listens on `http://localhost:5072` and `https://localhost:7211`. In `Development` the
Swagger UI is served at the root, so <http://localhost:5072/> is a browsable client.

A quick check once it is running:

```bash
curl http://localhost:5072/stories/10
```

---

## The Endpoint

`GET /stories/{n}` returns the *n* highest-scoring stories, ordered by score descending.

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

`n` must be between 1 and 500. If `n` is larger than the number of stories Hacker News currently
publishes, every available story is returned.

| Situation | Status |
|-----------|--------|
| Success | `200 OK` |
| `n` is zero, negative or above 500 | `400 Bad Request` |
| `n` is missing or non-numeric (no matching route) | `404 Not Found` |
| Hacker News unavailable or rate limiting | `503 Service Unavailable` |
| Hacker News timed out | `504 Gateway Timeout` |
| Hacker News returned an unexpected error | `502 Bad Gateway` |

Errors share one shape, and never leak stack traces:

```json
{ "error": "Hacker News is currently unavailable. Please try again later.", "code": "ExternalApiUnavailable" }
```

---

## Serving Load Without Overloading Hacker News

This is the requirement the design is built around. The guiding principle is that **the volume of
calls made to Hacker News is a function of time, not of inbound traffic** — a thousand requests per
second and one request per minute produce the same outbound load.

Four mechanisms combine to make that true:

**1. A background warm-up owns the data.**
`CacheWarmupService` reloads the whole working set on a fixed schedule (default: every 120s) and
writes it into the cache *before* the existing entries expire. Requests read what it has already
fetched. Steady-state cost to Hacker News is ~201 calls every two minutes — about 1.7 requests per
second — regardless of how busy the API is.

**2. Caching, with stampede protection.**
`MemoryCacheService` serves repeated reads, and guards each key with its own semaphore behind a
double-checked lock. When many callers miss the same key simultaneously, exactly one call goes to
Hacker News and the rest wait for its result. Different story IDs never block each other.

**3. A process-wide concurrency cap.**
`HackerNewsThrottle` is a singleton semaphore, applied by `ConcurrencyLimitingHandler` — a
delegating handler on the typed `HttpClient`. Because the cap sits in the handler chain, *every*
outbound call passes through it: the ID list, every story item, and the background warm-up alike.
No number of concurrent inbound requests can produce more than `MaxParallelRequests` simultaneous
calls to Hacker News. `SocketsHttpHandler.MaxConnectionsPerServer` enforces the same ceiling one
layer lower.

> The semaphore deliberately lives in a **singleton** rather than in the handler itself. Handler
> chains are pooled and rotated by `IHttpClientFactory`, so a semaphore owned by the handler would
> be silently recreated — and the limit quietly reset — on every rotation.

**4. Graceful degradation.**
A failed warm-up is logged and retried on the next cycle; it never faults the host, and previously
cached data keeps serving. A single story that fails to load is skipped rather than failing the
whole response. Only a failure to fetch the ID list itself fails the request.

### Measured

Against the live Hacker News API on a development machine, after warm-up:

| | |
|---|---|
| 1,000 concurrent requests (random `n` between 1 and 500) | **1.02s — 984 req/s, all `200 OK`** |
| Latency | p50 **50ms**, p95 **91ms**, p99 **107ms** |
| Calls made to Hacker News during those 1,000 requests | **0** |

`LoadBehaviourTests` asserts these properties deterministically against a stubbed Hacker News: 200
simultaneous requests for 50 stories each produce **61** outbound calls rather than 12,200, and
peak outbound concurrency never exceeds the configured cap.

---

## Architecture

Clean Architecture, with a strictly unidirectional dependency flow:

```
BestStories.API            → Controller, DTOs, app service, response mapping
BestStories.Application    → Use case, validation, repository interface, Result pattern
BestStories.Domain         → Story entity
BestStories.Infrastructure → Hacker News client, cache, throttle, background warm-up
```

API → Application → Domain. Infrastructure implements interfaces *defined in* Application, so the
dependency arrow points inward and the use case has no idea Hacker News is reached over HTTP.

`HackerNewsRepository`, `MemoryCacheService`, `HackerNewsThrottle` and `CacheWarmupService` are all
`internal sealed` — they are implementation detail, reachable only through their interfaces.

### Lifetimes

| Service | Lifetime | Why |
|---------|----------|-----|
| `IMemoryCache` | Singleton | Framework-managed shared state |
| `ICacheService` | Singleton | Must outlive a request — a per-request cache would be an empty cache |
| `HackerNewsThrottle` | Singleton | The concurrency cap is only a cap if it is shared process-wide |
| `GetBestStoriesValidator` | Singleton | Stateless; rules are built once |
| `IHackerNewsRepository` | Transient | `IHttpClientFactory` typed-client convention |
| `GetBestStoriesUseCase`, `IStoriesAppService` | Scoped | Per-request orchestration, no shared state |
| `CacheWarmupService` | Singleton (hosted) | Resolves the repository from a fresh scope each cycle |

---

## Design Decisions

### Every candidate is scored, not just a prefix of the list

`beststories.json` returns up to 500 IDs. The use case fetches **all** of them and sorts by score,
rather than taking the first *n* (or *n* plus a buffer) and sorting those.

Hacker News does not document that list as being ordered by score. It empirically is today, but
ranking only a prefix would make correctness depend on an undocumented implementation detail that
could change without warning. Scoring the full set is correct by construction, and it costs nothing
at request time: the set is bounded and permanently warm in cache, so it is a dictionary lookup per
story. It also means every value of `n` is answered from one shared cached set.

### Result pattern instead of exceptions for expected failures

`Result<T>` carries an `ErrorCode` that the API layer maps to a status code. Expected outcomes —
invalid input, an upstream outage — are values, not exceptions, so the use case reads as a single
straight-line flow. `Map` allows the app service to project the payload without unwrapping.

### Two deliberately different failure strategies

`FlurlRequestExecutor` exposes two methods, and which one a call uses is a design statement:

- `ExecuteAsync` **propagates**. Used for the ID list: without it there is nothing to return.
- `ExecuteWithFallbackAsync` **returns null**. Used for individual stories: one bad item is dropped
  and the caller still gets everything else.

`ExternalApiGuard` then translates those infrastructure exceptions into `Result` failures, so the
Application layer never sees a Flurl type.

### Hand-written mapping

Mapping is done with small extension methods (`ToStory`, `ToResponse`) rather than a mapping
library. The two mappings are a handful of fields whose only real content is the renames
(`url` to `uri`, `by` to `postedBy`, `descendants` to `commentCount`) and the Unix-seconds
conversion — all of which belong in plain sight. Doing it by hand makes a change to either type a
compile error rather than a runtime surprise, and avoids taking a commercially-licensed dependency
(AutoMapper requires a paid licence from v15 onward) for something this small.

### Cache abstraction

Nothing depends on `IMemoryCache` directly, only on `ICacheService`. Moving to Redis for a
multi-instance deployment means writing one class and changing one registration.

---

## Configuration

Everything sits under the `HackerNews` key in `appsettings.json`:

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `https://hacker-news.firebaseio.com/v0/` | Hacker News API base URL |
| `MaxStoriesCount` | `500` | Largest accepted `n`; larger values are rejected before any external call |
| `MaxParallelRequests` | `25` | **Process-wide** ceiling on simultaneous Hacker News calls |
| `HttpClientTimeoutSeconds` | `30` | Per-call HTTP timeout |
| `CacheWarmupEnabled` | `true` | Whether the background warm-up runs |
| `CacheWarmupIntervalSeconds` | `120` | Refresh cadence — this sets the steady-state load on Hacker News |
| `IdListCacheSeconds` | `600` | Safety-net TTL for the ID list |
| `StoryItemCacheSeconds` | `600` | Safety-net TTL for story details |

TTLs are a safety net rather than the primary mechanism: the warm-up replaces entries well before
they expire, so the TTLs only come into play if the warm-up is disabled or has been failing.

---

## Testing

89 tests, no network access required — the integration tests substitute the transport, so they are
deterministic and run offline.

```bash
dotnet test
```

**`BestStories.UnitTests`** covers the use case, validation, the `Result` pattern, error
translation, mapping, cache behaviour (including a concurrent-miss test proving stampede
protection), the concurrency handler, and the warm-up service.

**`BestStories.IntegrationTests`** boots the real application through `WebApplicationFactory` —
real controller, real use case, real cache, real throttle — with only the outermost HTTP hop
replaced by a stub. It asserts:

- the response matches the specification's field names, order and formats exactly, including
  `"time": "2019-10-12T13:43:01+00:00"`
- results are the true top *n* by score even when the highest scorer is last in the list
- invalid `n` is rejected without any call to Hacker News
- deleted, flagged and non-story items are excluded
- an outage surfaces as `503`, and a malformed upstream response as `502` — never `500`
- 200 concurrent requests collapse to one call per story
- peak outbound concurrency never exceeds the configured cap
- with the warm-up running, serving a request costs zero Hacker News calls

---

## Assumptions

- **`n` is a path parameter.** The specification says only that the caller specifies `n`;
  `GET /stories/10` reads naturally as REST. A query parameter would work equally well.
- **`n` is capped at 500**, matching the documented maximum size of the best-stories list. Asking
  for more is a client error rather than a silently clamped value. In practice Hacker News
  currently returns 200 IDs, so larger values return everything available.
- **`uri` is `null` for text posts.** Ask HN and similar entries have no `url`. They are genuine
  best stories, so they are returned with a null `uri` rather than being dropped or given a
  synthesised link.
- **Deleted, flagged (`dead`) and non-story items are excluded.** The best-stories list should only
  contain live stories, but the item endpoint can return other types, and filtering is cheap
  insurance against a malformed response.
- **Score ties keep the order Hacker News gave them.** No secondary sort key is specified, and
  `OrderByDescending` is a stable sort, so ties are deterministic run to run.
- **Serving data up to one warm-up cycle old is acceptable.** Story scores drift slowly; a ranking
  a couple of minutes stale is a good trade for the load characteristics it buys. The interval is
  configurable.
- **Single instance, in-memory cache.** No distributed cache is configured, which is the right
  default for one process. See below.

---

## Given More Time

| Item | Why |
|------|-----|
| **Distributed cache (Redis)** | With several instances, each keeps its own cache and multiplies the load on Hacker News by the instance count. `ICacheService` exists precisely so this is a one-class change. |
| **Resilience policies (Polly)** | Retry with jittered backoff, and a circuit breaker so a Hacker News outage fails fast instead of tying up the throttle. |
| **Serve stale on failure** | Keep the last known-good snapshot so an outage degrades to slightly-old data rather than `503`. |
| **Inbound rate limiting** | ASP.NET Core's rate limiter to protect *this* API, complementing the outbound protection already in place. |
| **OpenTelemetry** | Traces correlating inbound requests with outbound calls, plus metrics for cache hit ratio, warm-up duration and throttle saturation — the things worth alerting on. |
| **Health checks** | `/health` reporting warm-up freshness and cache population, for container orchestration. |
| **Containerisation** | A Dockerfile and compose file, so reviewers can run it without a .NET SDK. |
| **Load testing in CI** | The workflow in `.github/workflows/ci.yml` builds and tests on every push; the next step is turning the informal numbers above into a k6 or NBomber run with regression thresholds. |
| **Pagination** | `?page=` / `?pageSize=` for clients that want to walk the list rather than take the top *n*. |
