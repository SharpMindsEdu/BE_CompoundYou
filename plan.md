# CompoundYou — Backend Implementation Plan

## Context

CompoundYou is a multi-tenant SaaS skill-management and training platform for enterprises. It combines a **skill matrix**, **career frameworks**, **AI-assisted goal management**, **leadership insights**, **learning paths**, and **predictive talent analytics**, all built on a tenant-scoped data plane.

The backend lives at `C:\Users\Fabia\RiderProjects\BE_CompoundYou`. Phase 0 (cleanup of the legacy trading codebase) and Phase 1.1–1.4 (tenant/RBAC/org foundation, auth slices, org structure, audit + GDPR) are **delivered**. The plan that follows incorporates the user's revised 7-phase product structure, with the existing work mapped to Phase 1 and a new Phase 1.5 closing out the foundation by defining the core data models for Skill / Goal / Learning that later phases will animate.

Intended outcome: a backend that follows a clear product narrative — **Foundation → Skill Intelligence → Career Transparency → AI Goals → Leadership Intelligence → Learning Intelligence → Predictive Talent Intelligence → Enterprise Integrations** — with the first three phases forming a marketable MVP.

---

## Locked-in architecture decisions

1. **Tenant isolation: row-level.** `TenantId` column on every tenant-owned entity + EF Core global query filter. EmptyTenantId means "global/platform-owned" (shared catalogs).
2. **Shared catalogs: one table, nullable `TenantId`.** Applies to Skill, SkillCategory, QuizQuestion, LearningResource.
3. **RBAC: roles on `TenantMembership`.** Enum `TenantRole { Employee, Manager, TenantAdmin }`. Platform admin via `User.IsPlatformAdmin`. Resource-based handler for "is this employee in your reporting line".
4. **Tenant context: JWT `tid` claim → middleware → scoped `ICurrentTenant` → EF interceptors.**
5. **Trading code archived on `trading-archive` branch, removed from master.** ✅ Phase 0.
6. **Auth: OTP for MVP; `IAuthProvider` abstraction prepared for OIDC.** OIDC implementation lives in Phase 8.
7. **Background jobs: Hangfire with Postgres storage.** Introduced in Phase 5 (leadership insights computation).
8. **PII encryption: rely on Postgres at-rest + TLS + log redaction.** Column-level deferred until a customer requires it.
9. **Integrations isolated to Phase 8.** Foundation work (IAuthProvider, ExternalSourceId fields on Employee) is in place from Phase 1.

---

## Cross-cutting technical priorities (decide for from day 1)

These are not phase deliverables but architectural commitments that, if ignored early, force expensive rebuilds later:

- **Event-driven mindset.** Start with `MediatR.INotification` for in-process events (assessment validated, goal completed, role assignment changed). When a customer needs cross-service or async fanout, evolve to outbox pattern + message bus without re-modelling.
- **Skill Graph thinking.** Skill, SkillCategory, ParentSkill relations modelled as a graph from day 1. PostgreSQL recursive CTEs over `Skill.ParentSkillId` cover Phase 2–4 needs; if traversal complexity demands it later, the data already maps cleanly onto Neo4j or AGE.
- **AI Feature Store discipline.** From Phase 4 onwards, every AI-assisted decision (goal suggestion, coaching agenda, learning recommendation) persists the input feature snapshot, model identifier, and output. Replayable, auditable, and explainable from one schema.
- **Auditability for AI Decisions.** Reuse the existing `AuditLogEntry` table — AI commands become `IAuditable` with metadata JSON containing prompt hash + model + truncated output.
- **Explainability Layer.** AI responses always carry a `rationale` / `evidence` field that the UI surfaces. The API contracts make this non-optional; don't bolt it on later.

---

## Phase 0 — Cleanup & foundation ✅ DELIVERED (`40416a3`)

Trading code archived on `trading-archive`, deleted from master. Clean `00_Initial` migration. `Directory.Build.props` enforces nullable + warnings-as-errors. Build green, 0 warnings.

---

## Phase 1 — Foundation & Core Platform (4–6 weeks)

**Status:** 1.1–1.4 delivered. 1.5 (Core Data Models) is the remaining work.

### Phase 1.1 ✅ Tenancy / RBAC / Org foundation (`908ac51`)
- Domain markers `ITenantScoped`, `IAuditable`.
- Entities: `Tenant`, `TenantMembership`, `TenantInvitation`, `Department`, `Team`, `Employee`, `AuditLogEntry`; `User.IsPlatformAdmin`.
- `TenantStampingInterceptor`, reflection-driven global query filter, `TenantContextMiddleware`, named policies (`PlatformAdmin` / `TenantAdmin` / `Manager` / `Employee`), `EmployeeAccessHandler`, `AuditLogBehavior`.
- Migration `01_Tenancy_Org`. `TenantFilterGuardTests` architecture test.

### Phase 1.2 ✅ Tenants + Memberships + Auth slices (`44cdc1c`)
- Tenants (5 endpoints), TenantMemberships (5 endpoints), Auth (`Login` with picker, `SwitchTenant`).
- `IAuthProvider` abstraction + `OtpAuthProvider`. `TokenDto` carries `RequiresTenantSelection` + `AvailableTenants` when relevant.

### Phase 1.3 ✅ Departments + Teams + Employees slices (`ab3649d`)
- Departments (4), Teams (5 including `SetTeamManager`), Employees (9 including resource-based `EmployeeAccessHandler` gating).
- Manager cycle detection (32-hop bound).

### Phase 1.4 ✅ Audit + GDPR slices (`f2d62af`)
- `ListAuditEntries` (TenantAdmin scope).
- `RequestDataExport` streams a zip; `RequestErasure` pseudonymizes User + Employee + memberships, retains audit log with anonymized `ActorUserId`.

### Phase 1.5 — Core Data Models (~1 week, schema only)

**Goal:** Lock in the schema for Skill / Goal / Learning so later phases build features without further migrations. **No endpoints in this phase** — entities + EF configurations + migration + seed only.

**Entities to add:**

| Entity | Scoping | Key fields |
|---|---|---|
| `SkillCategory` | `TenantId?` null=global | Name, Description, IsActive |
| `Skill` | `TenantId?` null=global | SkillCategoryId, Name, Description, ParentSkillId?, IsActive |
| `SkillLevel` | implicit (via Skill) | SkillId, Order (1..N), Name, Description, PointsThreshold |
| `EmployeeSkillAssessment` | `TenantId` (`ITenantScoped`) | EmployeeId, SkillId, ClaimedSkillLevelId, ValidatedSkillLevelId?, ValidatedByEmployeeId?, ValidatedOn?, Status enum (`SelfAssessed`/`PendingValidation`/`Validated`/`Rejected`), Evidence |
| `Goal` | `TenantId` (`ITenantScoped`) | EmployeeId, AuthorEmployeeId, Title, Description, Period, TargetType enum (`CourseCount`/`SkillLevel`/`Custom`), TargetValue, CurrentValue, DueOn, Status enum (`Draft`/`Active`/`Completed`/`Archived`), TargetSkillId? |
| `GoalCheckIn` | `TenantId` (`ITenantScoped`) | GoalId, AuthorEmployeeId, Note, ProgressValue |
| `LearningResource` | `TenantId?` null=public | Title, Description, Type enum (`Video`/`Article`/`Course`/`ExternalLink`), Url?, MediaFileId?, EstimatedMinutes, PointsAwarded, IsActive |

**Migration:** `02_CoreDataModel` adds the 7 tables + unique/foreign-key indexes. Seed step inserts global `SkillCategory` rows (Technical / Soft Skills / Leadership / Compliance) and 20–30 commonly used global Skills with 3-level scales (Beginner / Advanced / Expert) via `Infrastructure/Seeds/SkillSeed.cs` invoked post-migration.

**Verification:**
- `dotnet build` green.
- `TenantFilterGuardTests` passes for the new `ITenantScoped` entities (EmployeeSkillAssessment, Goal, GoalCheckIn).
- New tables visible via `psql` against dev DB.
- Existing Phase 1.1–1.4 endpoints unaffected.

### Phase 1 Erfolgsmetriken
- Erstes Unternehmen onboardbar ✅
- Organisationsstruktur vollständig abbildbar ✅
- Nutzer können sich sicher anmelden ✅
- Skill / Goal / Learning Schema produktionsbereit (Phase 1.5)

---

## Phase 2 — Skill Intelligence MVP (6–8 weeks)

**Goal:** First real product value — skill matrix end-to-end on top of the Phase 1.5 schema.

### Deliverables

**Skill Framework Engine**
- Skill / SkillCategory / SkillLevel CRUD vertical slices.
- Tenant-private skills extend the global catalog; tenants can override level thresholds.
- 5-level default scale configurable per tenant.

**Employee Skill Profiles**
- Self-assessment, manager validation, validation status workflow (`SelfAssessed → PendingValidation → Validated/Rejected`).
- Resource-based authorization: employee may submit only their own assessment; only an upstream manager (any depth) or TenantAdmin can validate. Reuse `EmployeeAccessHandler`.

**Skill Matrix**
- Individual matrix (employee view of own skills).
- Team Skill Distribution (manager view: matrix per team).
- Department aggregation view (TenantAdmin view).

**Skill Gap Analysis (basic)**
- Compare an employee's actual skill levels against expected levels from their team's required skills (TeamSkillRequirement, added in Phase 3 — for Phase 2 stub against a placeholder list).

### Endpoints (new on top of Phase 1.5 schema)
- `Skills/Commands`: `CreateGlobalSkill` (PlatformAdmin), `CreateTenantSkill`, `UpdateSkill`, `AddSkillLevel`, `ReorderSkillLevels`, `DeactivateSkill`.
- `Skills/Queries`: `ListSkills`, `SearchSkills`, `GetSkillTree`.
- `SkillCategories`: CRUD.
- `EmployeeSkills/Commands`: `SubmitAssessment`, `ValidateAssessment`, `RejectAssessment`.
- `EmployeeSkills/Queries`: `GetMyMatrix`, `GetEmployeeMatrix` (manager), `GetTeamHeatmap`, `GetSkillGapReport`.
- Optional `MatrixHub` SignalR pushing `assessment-validated` events.

### Cross-cutting in Phase 2
- `ISkillCatalogService` with in-memory cache (5-min TTL) for global skills.
- Specifications: `SkillsVisibleToTenantSpec`, `EmployeeSkillGapSpec`.

### Erfolgsmetriken
- Erste Skill Assessments durchgeführt.
- Team Heatmaps generierbar (Performance: <1s auf 500-Employee-Tenant).
- Skill Gap Reports renderbar.

---

## Phase 3 — Career Transparency (4–6 weeks)

**Goal:** Make career development visible — role frameworks, career paths, promotion readiness.

### Entities (new)

| Entity | Scoping | Key fields |
|---|---|---|
| `JobFamily` | `TenantId` (`ITenantScoped`) | Name, Description, IsActive |
| `CareerLevel` | implicit | JobFamilyId, Order, Name, Description |
| `RoleProfile` | `TenantId` (`ITenantScoped`) | JobFamilyId, CareerLevelId, Name, Description, IsActive |
| `RoleProfileSkillRequirement` | implicit | RoleProfileId, SkillId, RequiredSkillLevelId, Weight |
| `EmployeeRoleProfile` | implicit | EmployeeId, RoleProfileId, AssignedOn, IsActive |
| `TeamSkillRequirement` | implicit | TeamId, SkillId, RequiredSkillLevelId (optional team-level override) |
| `CareerPathSnapshot` | `TenantId` (`ITenantScoped`) | EmployeeId, CurrentRoleProfileId, TargetRoleProfileId, ReadinessScore (0–100), ScoredOn |

### Deliverables
- **Role Frameworks:** Job Families, Career Levels, Role Profile CRUD; Set Skill Requirements per role.
- **Career Path Engine:** for each employee shows current position, optional target role, skill gaps (delta between current assessed levels and target's required levels), Readiness Score.
- **Promotion Readiness scoring:** weighted combination of Skill Fit (Phase 2 data) + Goal Completion (Phase 4 schema is in place, behaviors empty until Phase 4) + Manager Validation count.

### Endpoints
- `JobFamilies`, `CareerLevels`, `RoleProfiles`: CRUD (TenantAdmin).
- `RoleProfileSkillRequirements/Commands`: `SetRequirement`, `RemoveRequirement`, `BulkSetRequirements`.
- `EmployeeRoleProfiles/Commands`: `AssignProfile`, `UnassignProfile`.
- `TeamSkillRequirements`: CRUD.
- `CareerPaths/Queries`: `GetMyCareerPath`, `GetEmployeeCareerPath` (manager + access handler), `GetPromotionReadiness`, `GetTeamReadinessSummary`.

### Migration
`03_CareerFramework` adds the 7 tables + the Phase 2 Skill Gap query is upgraded to use real `TeamSkillRequirement` data.

### Erfolgsmetriken
- Rollenmodelle vollständig konfigurierbar.
- Karrierepfade sichtbar.
- Readiness Scores berechnet und stabil über Re-Scoring.

---

## Phase 4 — AI Goal Intelligence (4–5 weeks)

**Goal:** Intelligent goal management — schema already in place from Phase 1.5; this phase adds behaviors and AI assistance.

### Deliverables

**Goal Management** (full lifecycle on existing `Goal`/`GoalCheckIn`)
- Goal types via `Goal.Period` + `TargetType`: Quarterly, Annual, Development, Career.
- Co-authoring semantics: employee proposes → manager approves → both can check-in.

**AI Goal Assistant**
- Suggest goals based on role profile, skill gaps, career path target.
- Goal quality scoring (SMART / OKR conformance) via LLM.
- Goal-to-Skill mapping via existing `Goal.TargetSkillId`.

### Cross-cutting additions
- `IAiAssistant` abstraction with `OpenAiAssistant` initial implementation (configurable provider).
- AI Feature Store table: `AiInteraction` (TenantId, ActorUserId, Kind, InputSnapshotJsonb, Model, OutputJsonb, RationaleJsonb, OccurredOn). Reused in Phases 5 + 6 + 7.
- AI Decision auditing: `AuditLogEntry.MetadataJson` includes model id + prompt hash + interaction id linking to `AiInteraction`.
- Explainability: all AI command responses include a `rationale` field; UI surfaces it inline with the suggestion.

### Endpoints
- `Goals/Commands`: `CreateGoal`, `UpdateGoal`, `ApproveGoal`, `AddCheckIn`, `CompleteGoal`, `ArchiveGoal`.
- `Goals/Queries`: `GetMyGoals`, `GetTeamGoals`, `GetGoalsBySkill`, `GetGoalProgress`.
- `Ai/Commands`: `SuggestGoalsForEmployee`, `RateGoalQuality`, `RewriteGoalAsSmart`.
- `Ai/Queries`: `GetInteractionHistory` (TenantAdmin, for audit + tuning).

### Migration
`04_AiAndGoals` adds `AiInteraction`. Goals schema already exists from Phase 1.5.

### Erfolgsmetriken
- Ziele werden aktiv erstellt.
- Skill-linked Goals vorhanden (KPI: % goals with TargetSkillId set).
- AI-Vorschläge akzeptiert (Annahmequote).

---

## Phase 5 — Leadership Intelligence (5–6 weeks)

**Goal:** Empower managers with proactive insights and AI coaching support.

### Entities (new)

| Entity | Scoping | Key fields |
|---|---|---|
| `LeadershipInsight` | `TenantId` (`ITenantScoped`) | ManagerEmployeeId, TargetEmployeeId?, TargetTeamId?, Kind enum (`SkillRisk`/`Stagnation`/`HighPotential`/`PromotionCandidate`), Severity, DetailJsonb, OccurredOn, DismissedOn? |
| `CoachingSession` | `TenantId` (`ITenantScoped`) | ManagerEmployeeId, EmployeeId, ScheduledFor, AgendaJsonb, NotesJsonb, CompletedOn? |

### Deliverables

**Leadership Cockpit**
- Manager dashboard summarising Team Skill Risks, Stagnierende Entwicklung, High Potentials, Promotion Candidates.

**AI Coaching Assistant**
- Generate suggested 1:1 talking points, coaching questions, feedback guidelines from employee context (skills + goals + assessments).
- Pre-fills `CoachingSession.AgendaJsonb`; the manager edits before the meeting.

**Risk Detection**
- Hangfire recurring job runs weekly, evaluates rules (sinkende Aktivität, fehlende Entwicklung, kritische Skill Gaps) and creates `LeadershipInsight` rows.
- Manager receives in-app notification via existing `Notification` infrastructure (introduced here).

### Cross-cutting additions
- **Hangfire** (`Hangfire.AspNetCore` + `Hangfire.PostgreSql`) introduced. Dashboard gated to `PlatformAdmin`.
- `INotificationSender` (in-app first; email plug-in via MailKit for Phase 8 enterprise hardening).

### Endpoints
- `LeadershipInsights/Queries`: `GetCockpit`, `GetMyInsights`, `GetTeamInsights`.
- `LeadershipInsights/Commands`: `DismissInsight`, `AcknowledgeInsight`.
- `Coaching/Commands`: `GenerateAgenda` (AI), `CreateSession`, `UpdateAgenda`, `AddNotes`, `CompleteSession`.
- `Coaching/Queries`: `GetUpcomingSessions`, `GetSessionHistory`.

### Migration
`05_LeadershipIntelligence` adds 2 tables + Hangfire's own migrations apply on first start.

### Erfolgsmetriken
- Manager nutzen Insights vor Gesprächen.
- Coaching Sessions vorbereitet (KPI: % sessions with AI-generated agenda).

---

## Phase 6 — Learning Intelligence (4–6 weeks)

**Goal:** Operationalize skill development on the Phase 1.5 `LearningResource` schema.

### Entities (new, alongside existing `LearningResource`)

| Entity | Scoping | Key fields |
|---|---|---|
| `LearningPath` | `TenantId?` null=public | Title, Description, TargetSkillId?, IsActive |
| `LearningPathItem` | implicit | LearningPathId, LearningResourceId, Order, IsRequired |
| `LearningCompletion` | `TenantId` (`ITenantScoped`) | EmployeeId, LearningResourceId, CompletedOn, PointsEarned, Source enum (`Manual`/`External`) |
| `KnowledgeContribution` | `TenantId` (`ITenantScoped`) | AuthorEmployeeId, Title, Content, SkillId?, EndorseCount |

### Deliverables

**Content Layer**
- Link documents, videos, internal best practices, external courses (LinkedIn Learning, Udemy, Moodle) — by URL initially. SCORM ingestion deferred to Phase 8 unless a pilot customer requires it.

**AI Learning Recommendations**
- Recommend `LearningResource` / `LearningPath` items based on Skill Gaps (Phase 2), Career Goals (Phase 4), Team Priorities (Phase 5). Reuses `IAiAssistant` + `AiInteraction`.

**Social Learning**
- Expert Finder: top employees per skill (validated, level ≥ threshold), surfaced via Skill detail and search.
- Peer Recommendations: signal when a colleague endorses a resource.
- Knowledge Contributions: employees publish short articles tagged with a Skill; counted as a contribution metric.

### Endpoints
- `LearningResources`: CRUD + `ImportFromUrl`.
- `LearningPaths`: CRUD + `ReorderItems`.
- `Learning/Commands`: `MarkResourceCompleted`, `EndorseResource`, `RecommendForEmployee` (AI).
- `Learning/Queries`: `GetMyRecommendations`, `GetExpertsForSkill`, `GetPathProgress`, `GetTeamLearningSummary`.
- `KnowledgeContributions`: CRUD + endorse.

### Migration
`06_LearningIntelligence` adds 4 tables. Existing `LearningResource` already present from Phase 1.5.

### Erfolgsmetriken
- Lerninhalte werden konsumiert.
- Skill-linked Learning Journeys vorhanden.
- Expert Finder Nutzung (Queries / Woche).

---

## Phase 7 — Predictive Talent Intelligence (6–8 weeks)

**Goal:** Strategic HR insights — workforce analytics, forecasts, executive dashboards.

### Entities (new)

| Entity | Scoping | Key fields |
|---|---|---|
| `WorkforceSnapshot` | `TenantId` (`ITenantScoped`) | OccurredOn, MetricsJsonb (skill distribution, gap counts, mobility metrics) |
| `TalentForecast` | `TenantId` (`ITenantScoped`) | GeneratedOn, Kind enum (`SkillGap`/`HiringNeed`/`SuccessionRisk`), TargetRoleProfileId?, Severity, Confidence, DetailJsonb |
| `SuccessionPlan` | `TenantId` (`ITenantScoped`) | RoleProfileId, CandidatesJsonb (employeeId + readiness + rank), UpdatedOn |

### Deliverables

**Workforce Analytics**
- Skill Trends (rising / declining levels across tenant).
- Skill Scarcity (skills with high required-vs-validated gap).
- Internal Mobility Potential (candidates ready for adjacent roles).

**Forecasting Models**
- Predicted critical Skill Gaps over 6 / 12 months (initially rule-based projection; ML-ready data via `AiInteraction` + `WorkforceSnapshot`).
- Hiring needs (where forecasted demand exceeds internal readiness).
- Succession risks (key roles without ready successors).

**Executive Dashboard**
- C-Level KPIs: Talent Readiness Index, Leadership Pipeline strength, Internal Mobility Index.

### Cross-cutting additions
- Hangfire nightly job snapshots `WorkforceSnapshot` rows.
- Forecasting service: rule-based v1, ML-ready abstraction so a model can be slotted in.
- BI export (CSV / Parquet) endpoints scoped to `PlatformAdmin` / `TenantAdmin`.

### Endpoints
- `Analytics/Queries`: `GetSkillTrends`, `GetSkillScarcity`, `GetInternalMobilityIndex`, `GetTalentReadiness`.
- `Forecasts/Queries`: `ListForecasts`, `GetForecast`.
- `Succession/Queries`: `GetSuccessionPlan`, `GetSuccessionGaps`.
- `BiExport/Queries`: `StreamSkillsCsv`, `StreamGoalsCsv`, `StreamWorkforceSnapshotCsv`.

### Migration
`07_PredictiveTalent` adds 3 tables + scheduled-job entries.

### Erfolgsmetriken
- Strategische Reports nutzbar.
- HR-Entscheidungen datenbasiert (KPI: # exec dashboard views / month).

---

## Phase 8 — Enterprise Integrations & Hardening (4–6 weeks)

**Goal:** Enterprise-ready integrations, observability, and production hardening. Triggered by first paying customer.

### Deliverables

**Personio HR Sync**
- `IPersonioClient` (Refit), `PersonioSyncJob` recurring nightly via Hangfire, reconciles employees / departments / teams / managers by `Employee.ExternalSourceId` (column already present from Phase 1.1).
- Per-tenant credentials in new `TenantIntegrationCredentials` table encrypted with `IDataProtector`.

**OIDC SSO**
- Plug second `IAuthProvider` — `OidcAuthProvider` via `Microsoft.AspNetCore.Authentication.OpenIdConnect`. Targets Microsoft Entra ID + Google Workspace.
- Per-tenant `OidcEnabled` + issuer / clientId / clientSecret. JIT-provision User + Employee on first login matched by email.

**SAML 2.0**
- Optional, only on specific customer demand. `Sustainsys.Saml2` integration.

**SCORM ingestion**
- Deferred until a learning-content-heavy customer requires it.

**BI Export**
- Already implemented in Phase 7; this phase exposes it via scheduled S3 / Azure Blob drops per tenant.

**Production hardening**
- `Microsoft.AspNetCore.RateLimiting` per-tenant.
- Serilog with PII-redacting enricher + OpenTelemetry exporters.
- Health checks `/healthz` (DB + Hangfire + IdP).
- RFC 7807 structured error responses.
- Security headers middleware (CSP, HSTS, X-Frame-Options).
- JWT key rotation provider abstraction.
- Backup + restore runbook.

### Erfolgsmetriken
- Personio Sandbox-Sync grün gegen 500+ Employees.
- OIDC E2E gegen Entra ID Test-Tenant.
- Health-Check + Rate-Limit + RFC-7807 in Smoke-Tests abgedeckt.

---

## Go-to-market sequencing (per the user's product plan)

- **MVP** = Phase 0 + Phase 1 + Phase 2 + Phase 3 → Skill Transparenz, Skill Gap Analyse, Karrierepfade, Management Dashboards. An AI-powered Talent Intelligence MVP.
- **V2** = + Phase 4 + Phase 5 → AI differentiation (goal intelligence + leadership intelligence).
- **V3** = + Phase 6 + Phase 7 → Platform dominance / enterprise expansion.
- **Phase 8** ramps in parallel once V1 customers commit.

---

## Existing functions/patterns to reuse (do not re-invent)

- `IRepository<T>` + `Repository<T>` — Domain/Repositories, Infrastructure/Repositories.
- `TrackedEntity` (`CreatedOn`/`UpdatedOn`/`DeletedOn`) — Domain/Entities.
- `ApplicationDbContext` — reflection-driven global query filter and stamping interceptor already wired.
- `TransactionBehavior` / `ValidationBehavior` / `AuditLogBehavior` — MediatR pipeline.
- `Result<T>` (`IOperationResult` interface) + `ResultExtensions.ToHttpResult` — Application/Shared.
- `TokenService` (extended in Phase 1.2 for tenant claims) + `ClaimExtensions`.
- `LocalFileStorage` (`IFileStorage`) — used by GDPR export, will be reused for Phase 6 learning attachments.
- `ICurrentTenant` (Phase 1.1) — inject into any handler that needs the active tenant or actor.
- `EmployeeAccessHandler` (Phase 1.1) — resource-based "can this user access this Employee" check.
- `IAuditLogger` + `IAuditable` marker — every state-changing command implements `IAuditable` and gets an audit row for free.
- `IAuthProvider` (Phase 1.2) — drop-in slot for OIDC in Phase 8.

---

## Critical files / locations going forward

- `Domain/Entities/` — Phase 1.5 adds Skill, SkillCategory, SkillLevel, EmployeeSkillAssessment, Goal, GoalCheckIn, LearningResource.
- `Infrastructure/Configurations/Skills/`, `Goals/`, `Learning/` — new EF configuration folders.
- `Infrastructure/Seeds/SkillSeed.cs` — global catalog seed invoked post-migration.
- `Infrastructure/Migrations/` — sequential migrations `02_CoreDataModel`, `03_CareerFramework`, `04_AiAndGoals`, `05_LeadershipIntelligence`, `06_LearningIntelligence`, `07_PredictiveTalent`.
- `Application/Features/{Skills,SkillCategories,EmployeeSkills}` — Phase 2 slices.
- `Application/Features/{JobFamilies,CareerLevels,RoleProfiles,CareerPaths,TeamSkillRequirements}` — Phase 3 slices.
- `Application/Features/{Goals,Ai}` + `Application/Services/IAiAssistant` — Phase 4.
- `Application/Features/{LeadershipInsights,Coaching}` + Hangfire jobs in `Infrastructure/Jobs` — Phase 5.
- `Application/Features/{LearningResources,LearningPaths,Learning,KnowledgeContributions}` — Phase 6.
- `Application/Features/{Analytics,Forecasts,Succession,BiExport}` — Phase 7.
- `Infrastructure/Integrations/Personio/`, `Infrastructure/Services/OidcAuthProvider.cs` — Phase 8.

---

## Verification strategy (end-to-end)

- **Per-phase integration tests** via `Tests/Unit.Tests/` using `PostgreSqlRepositoryTestDatabaseFixture` (Testcontainers).
- **Tenant isolation guard:** `TenantFilterGuardTests` (Phase 1.1) — re-runs in CI; fails when a new `ITenantScoped` entity lacks a filter. Every phase 1.5–7 entity that is tenant-owned must implement the marker.
- **Cross-tenant leak test:** seed two tenants, log in as A, run each list/detail endpoint, assert zero B rows leak. Grows per phase.
- **Authorization matrix test:** parametrised test exercising every endpoint with `{PlatformAdmin, TenantAdmin, Manager, Employee, Anonymous}` and asserting the expected status code.
- **AI auditability test (Phase 4+):** every AI command writes both an `AiInteraction` row and a linked `AuditLogEntry`.
- **Manual smoke per phase via Scalar UI (`/scalar/v1`)**:
    - Phase 1.5: tables visible via psql; schema seed succeeded.
    - Phase 2: skill catalog → assessment → validation → matrix + gap report.
    - Phase 3: job family → role profile → assignment → readiness score.
    - Phase 4: AI-suggested goal → quality rating → check-in → completion.
    - Phase 5: trigger weekly insight job → manager dashboard populated → coaching agenda generated.
    - Phase 6: import URL → assign in learning path → mark completion → expert appears in search.
    - Phase 7: nightly snapshot → forecast generated → exec dashboard renders.
    - Phase 8: Personio sandbox sync → OIDC login round-trip → health check + rate-limit responses.
- **Pre-pilot dress rehearsal (after Phase 3 = MVP):** full happy-path against a Personio-seeded tenant (manual import for the rehearsal; automated in Phase 8).