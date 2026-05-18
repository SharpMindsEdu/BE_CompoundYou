-- Test fixture: Phase 1 + Phase 2 + Phase 3 frontend workspace.
--
-- Idempotent: re-run any time to reset the fixture tenants, users, org
-- structure, skill catalog, assessments, career frameworks, audit rows, goals
-- and learning rows.
--
-- Usage (from a psql shell against the dev database):
--   \i Tests/Fixtures/test-user-multi-tenant.sql
--
-- Primary login flow against the running backend:
--   1. PUT /api/users/login
--        body: { "email": "test@compoundyou.local", "code": "123456" }
--      (skip RequestLogin; the OTP is pre-set below)
--   2. Response requires tenant selection.
--   3. POST /api/users/switch-tenant
--        body: { "tenantId": <Acme Corp or Globex Industries id> }
--
-- Useful test accounts, all with code 123456:
--   test@compoundyou.local             Acme TenantAdmin, Globex Manager
--   maya.manager@compoundyou.local     Acme Manager
--   jonas.employee@compoundyou.local   Acme Employee
--   zoe.ops@compoundyou.local          Globex Employee
--
-- Notes:
--   Phase 3 TeamSkillRequirement rows intentionally make Phase 2 gap reports
--   non-empty for employees whose team has required skill levels configured.

BEGIN;

-- Stable high IDs keep tenant-bound JWT membership IDs aligned with employee
-- IDs. Some Phase 2 handlers currently use the "mid" claim as an EmployeeId.
-- The fixture therefore gives each tenant_membership the same id as its
-- corresponding employee row.

-- 1) Wipe prior fixture state. Tenant cascades clear departments, teams,
-- employees, memberships, invitations, skills, levels, assessments, goals and
-- learning resources. Audit rows are not FK-bound, so remove them explicitly.
CREATE TEMP TABLE _fixture_tenant_ids (id bigint PRIMARY KEY) ON COMMIT DROP;
INSERT INTO _fixture_tenant_ids (id)
SELECT id
FROM tenant
WHERE id IN (910001, 910002)
   OR slug IN ('acme-corp', 'globex-industries');

CREATE TEMP TABLE _fixture_user_ids (id bigint PRIMARY KEY) ON COMMIT DROP;
INSERT INTO _fixture_user_ids (id)
SELECT id
FROM "user"
WHERE id BETWEEN 900001 AND 900199
   OR email LIKE '%@compoundyou.local';

DELETE FROM audit_log_entry
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   OR actor_user_id IN (SELECT id FROM _fixture_user_ids);

DELETE FROM career_path_snapshot
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   OR employee_id IN (
        SELECT id FROM employee WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   );

DELETE FROM employee_role_profile
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   OR employee_id IN (
        SELECT id FROM employee WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   );

DELETE FROM role_profile_skill_requirement
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM team_skill_requirement
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM role_profile
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM career_level
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM job_family
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM employee_skill_assessment
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM goal_check_in
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   OR goal_id IN (
        SELECT id FROM goal WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   );

DELETE FROM goal
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM learning_resource
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM skill_level
WHERE skill_id IN (
    SELECT id FROM skill WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
);

UPDATE skill
SET parent_skill_id = NULL
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM skill
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM skill_category
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM tenant_invitation
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM tenant_membership
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   OR user_id IN (SELECT id FROM _fixture_user_ids);

UPDATE team
SET manager_employee_id = NULL
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

UPDATE employee
SET manager_employee_id = NULL,
    team_id = NULL
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   OR user_id IN (SELECT id FROM _fixture_user_ids);

DELETE FROM employee
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids)
   OR user_id IN (SELECT id FROM _fixture_user_ids);

DELETE FROM team
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

UPDATE department
SET parent_department_id = NULL
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM department
WHERE tenant_id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM tenant
WHERE id IN (SELECT id FROM _fixture_tenant_ids);

DELETE FROM "user"
WHERE id IN (SELECT id FROM _fixture_user_ids);

-- 2) Users.
INSERT INTO "user" (
    id, display_name, email, phone_number,
    sign_in_secret, sign_in_tries, is_platform_admin,
    created_on, updated_on
)
VALUES
    (900001, 'Alex Admin',       'test@compoundyou.local',           NULL, '123456', 3, false, now(), now()),
    (900002, 'Maya Morgan',      'maya.manager@compoundyou.local',   NULL, '123456', 3, false, now(), now()),
    (900003, 'Jonas Reed',       'jonas.employee@compoundyou.local', NULL, '123456', 3, false, now(), now()),
    (900004, 'Lina Chen',        'lina.employee@compoundyou.local',  NULL, '123456', 3, false, now(), now()),
    (900005, 'Noor Patel',       'noor.manager@compoundyou.local',   NULL, '123456', 3, false, now(), now()),
    (900006, 'Theo Braun',       'theo.employee@compoundyou.local',  NULL, '123456', 3, false, now(), now()),
    (900101, 'Zoe Kim',          'zoe.ops@compoundyou.local',        NULL, '123456', 3, false, now(), now()),
    (900102, 'Omar Silva',       'omar.ops@compoundyou.local',       NULL, '123456', 3, false, now(), now());

-- 3) Tenants + memberships.
INSERT INTO tenant (id, name, slug, status, plan, owner_user_id, created_on, updated_on)
VALUES
    (910001, 'Acme Corp',          'acme-corp',          0, 'Pro',      900001, now(), now()),
    (910002, 'Globex Industries',  'globex-industries',  0, 'Business', 900001, now(), now());

-- Role enum: 0 = Employee, 1 = Manager, 2 = TenantAdmin.
INSERT INTO tenant_membership (
    id, tenant_id, user_id, role, joined_on, is_active, created_on, updated_on
)
VALUES
    (950001, 910001, 900001, 2, now() - interval '120 days', true, now(), now()),
    (950002, 910001, 900002, 1, now() - interval '95 days',  true, now(), now()),
    (950003, 910001, 900003, 0, now() - interval '80 days',  true, now(), now()),
    (950004, 910001, 900004, 0, now() - interval '76 days',  true, now(), now()),
    (950005, 910001, 900005, 1, now() - interval '70 days',  true, now(), now()),
    (950006, 910001, 900006, 0, now() - interval '55 days',  true, now(), now()),
    (950101, 910002, 900001, 1, now() - interval '45 days',  true, now(), now()),
    (950102, 910002, 900101, 0, now() - interval '35 days',  true, now(), now()),
    (950103, 910002, 900102, 0, now() - interval '30 days',  true, now(), now());

-- 4) Phase 1 org foundation: departments, teams, employees and invitations.
INSERT INTO department (id, tenant_id, name, parent_department_id, created_on, updated_on)
VALUES
    (920001, 910001, 'Product & Engineering', NULL,   now() - interval '110 days', now()),
    (920002, 910001, 'People Operations',     NULL,   now() - interval '108 days', now()),
    (920003, 910001, 'Platform Engineering',  920001, now() - interval '104 days', now()),
    (920004, 910001, 'Data & Insights',       920001, now() - interval '100 days', now()),
    (920101, 910002, 'Operations',            NULL,   now() - interval '44 days',  now());

INSERT INTO team (id, tenant_id, department_id, name, manager_employee_id, created_on, updated_on)
VALUES
    (930001, 910001, 920003, 'Frontend Platform', NULL, now() - interval '100 days', now()),
    (930002, 910001, 920004, 'Data Products',     NULL, now() - interval '98 days',  now()),
    (930003, 910001, 920002, 'Talent Enablement', NULL, now() - interval '96 days',  now()),
    (930101, 910002, 920101, 'Operational Excellence', NULL, now() - interval '40 days', now());

INSERT INTO employee (
    id, tenant_id, user_id, employee_number, first_name, last_name, email,
    date_of_birth, hire_date, team_id, manager_employee_id, external_source_id,
    is_active, created_on, updated_on
)
VALUES
    (950001, 910001, 900001, 'ACME-001', 'Alex', 'Admin',  'test@compoundyou.local',
     '1988-04-12', '2021-02-01', 930001, NULL,   'fixture-acme-alex', true, now() - interval '120 days', now()),
    (950002, 910001, 900002, 'ACME-014', 'Maya', 'Morgan', 'maya.manager@compoundyou.local',
     '1990-11-03', '2022-08-15', 930001, NULL,   'fixture-acme-maya', true, now() - interval '95 days', now()),
    (950003, 910001, 900003, 'ACME-027', 'Jonas', 'Reed',  'jonas.employee@compoundyou.local',
     '1996-06-22', '2023-04-03', 930001, NULL,   'fixture-acme-jonas', true, now() - interval '80 days', now()),
    (950004, 910001, 900004, 'ACME-031', 'Lina', 'Chen',   'lina.employee@compoundyou.local',
     '1994-01-19', '2023-06-12', 930001, NULL,   'fixture-acme-lina', true, now() - interval '76 days', now()),
    (950005, 910001, 900005, 'ACME-044', 'Noor', 'Patel',  'noor.manager@compoundyou.local',
     '1987-09-08', '2020-10-01', 930002, NULL,   'fixture-acme-noor', true, now() - interval '70 days', now()),
    (950006, 910001, 900006, 'ACME-052', 'Theo', 'Braun',  'theo.employee@compoundyou.local',
     '1998-03-27', '2024-01-08', 930002, NULL,   'fixture-acme-theo', true, now() - interval '55 days', now()),
    (950101, 910002, 900001, 'GLOBEX-001', 'Alex', 'Admin', 'test@compoundyou.local',
     '1988-04-12', '2024-03-01', 930101, NULL,   'fixture-globex-alex', true, now() - interval '45 days', now()),
    (950102, 910002, 900101, 'GLOBEX-009', 'Zoe', 'Kim',    'zoe.ops@compoundyou.local',
     '1992-12-02', '2024-04-01', 930101, NULL,   'fixture-globex-zoe', true, now() - interval '35 days', now()),
    (950103, 910002, 900102, 'GLOBEX-012', 'Omar', 'Silva', 'omar.ops@compoundyou.local',
     '1995-07-17', '2024-04-15', 930101, NULL,   'fixture-globex-omar', true, now() - interval '30 days', now());

UPDATE team SET manager_employee_id = 950001 WHERE id = 930001;
UPDATE team SET manager_employee_id = 950005 WHERE id = 930002;
UPDATE team SET manager_employee_id = 950001 WHERE id = 930003;
UPDATE team SET manager_employee_id = 950101 WHERE id = 930101;

UPDATE employee SET manager_employee_id = 950001 WHERE id IN (950002, 950003, 950005);
UPDATE employee SET manager_employee_id = 950002 WHERE id = 950004;
UPDATE employee SET manager_employee_id = 950005 WHERE id = 950006;
UPDATE employee SET manager_employee_id = 950101 WHERE id IN (950102, 950103);

INSERT INTO tenant_invitation (
    id, tenant_id, email, role, token, expires_on, accepted_on, accepted_by_user_id,
    created_on, updated_on
)
VALUES
    (955001, 910001, 'future.designer@compoundyou.local', 0, 'fixture-acme-invite-designer',
     now() + interval '14 days', NULL, NULL, now() - interval '2 days', now()),
    (955002, 910001, 'accepted.coach@compoundyou.local', 1, 'fixture-acme-invite-coach',
     now() + interval '14 days', now() - interval '1 day', 900001, now() - interval '7 days', now());

-- 5) Phase 1.5/2 skill catalog. Tenant-specific data avoids collisions with
-- the global seed while still exercising catalog, hierarchy and level UI.
INSERT INTO skill_category (id, tenant_id, name, description, is_active, created_on, updated_on)
VALUES
    (960001, 910001, 'Engineering Practices', 'Delivery skills used by product engineering teams.', true, now(), now()),
    (960002, 910001, 'Product & Collaboration', 'Discovery, communication and facilitation skills.', true, now(), now()),
    (960003, 910001, 'Leadership & Enablement', 'Manager and enablement capabilities.', true, now(), now()),
    (960101, 910002, 'Operations Excellence', 'Process and analytics capabilities for Globex.', true, now(), now());

INSERT INTO skill (
    id, tenant_id, skill_category_id, name, description, parent_skill_id,
    is_active, created_on, updated_on
)
VALUES
    (970001, 910001, 960001, 'Frontend Engineering', 'Build maintainable, accessible client experiences.', NULL,   true, now(), now()),
    (970002, 910001, 960001, 'Angular Delivery',     'Ship Angular/Ionic features with clean state and API integration.', 970001, true, now(), now()),
    (970003, 910001, 960001, 'API Contract Design',  'Design typed API contracts and resilient client integrations.', NULL, true, now(), now()),
    (970004, 910001, 960001, 'PostgreSQL Reporting', 'Model and query product analytics data in PostgreSQL.', NULL, true, now(), now()),
    (970005, 910001, 960002, 'Workshop Facilitation','Lead crisp discovery, retro and calibration sessions.', NULL, true, now(), now()),
    (970006, 910001, 960003, 'People Leadership',    'Coach people, validate growth and create accountability.', NULL, true, now(), now()),
    (970007, 910001, 960003, 'Skill Matrix Design',  'Translate capabilities into usable matrices and heatmaps.', NULL, true, now(), now()),
    (970101, 910002, 960101, 'Process Automation',   'Automate repeatable operating workflows.', NULL, true, now(), now()),
    (970102, 910002, 960101, 'Operational Analytics','Turn operations metrics into team decisions.', NULL, true, now(), now());

INSERT INTO skill_level (
    id, tenant_id, skill_id, "order", name, description, points_threshold, is_active, created_on, updated_on
)
VALUES
    (980001, 910001, NULL, 1, 'Foundation',   'Understands patterns and can contribute with guidance.', 0,   true, now(), now()),
    (980002, 910001, NULL, 2, 'Practitioner', 'Owns features and makes good trade-offs.',              100, true, now(), now()),
    (980003, 910001, NULL, 3, 'Expert',       'Shapes architecture, mentors others, and defines standards.', 250, true, now(), now()),
    (980101, 910002, NULL, 1, 'Foundation',   'Understands the operating model and contributes with guidance.', 0, true, now(), now()),
    (980102, 910002, NULL, 2, 'Practitioner', 'Owns repeatable workflows and improves team outcomes.', 100, true, now(), now()),
    (980103, 910002, NULL, 3, 'Expert',       'Leads operating standards and scales practices across teams.', 250, true, now(), now());

-- 6) Phase 2 employee skill profiles.
-- Status enum: 0 = SelfAssessed, 1 = PendingValidation, 2 = Validated, 3 = Rejected.
INSERT INTO employee_skill_assessment (
    id, tenant_id, employee_id, skill_id, claimed_skill_level_id,
    validated_skill_level_id, validated_by_employee_id, validated_on,
    status, evidence, created_on, updated_on
)
VALUES
    (990001, 910001, 950001, 970001, 980002, 980002, 950001, now() - interval '16 days', 2, 'Led the new skill matrix shell and API integration.', now() - interval '20 days', now()),
    (990002, 910001, 950001, 970006, 980003, 980003, 950001, now() - interval '15 days', 2, 'Owns team development cadence and validation rituals.', now() - interval '20 days', now()),
    (990003, 910001, 950001, 970005, 980002, NULL,   NULL,   NULL,                    1, 'Prepared a cross-functional calibration workshop.', now() - interval '4 days', now()),
    (990004, 910001, 950002, 970001, 980003, 980003, 950001, now() - interval '12 days', 2, 'Refactored shared frontend components and mentored peers.', now() - interval '18 days', now()),
    (990005, 910001, 950002, 970002, 980003, 980003, 950001, now() - interval '11 days', 2, 'Delivered complex Angular flows end-to-end.', now() - interval '18 days', now()),
    (990006, 910001, 950002, 970006, 980002, NULL,   NULL,   NULL,                    1, 'New people-lead role, awaiting calibration.', now() - interval '3 days', now()),
    (990007, 910001, 950003, 970001, 980002, 980002, 950001, now() - interval '9 days',  2, 'Owned profile page polish and error handling.', now() - interval '14 days', now()),
    (990008, 910001, 950003, 970002, 980002, NULL,   NULL,   NULL,                    1, 'Implemented Ionic form work, wants validation.', now() - interval '2 days', now()),
    (990009, 910001, 950003, 970003, 980001, NULL,   NULL,   NULL,                    0, 'Started contributing to generated API clients.', now() - interval '12 days', now()),
    (990010, 910001, 950004, 970001, 980001, 980001, 950002, now() - interval '8 days',  2, 'First shipped onboarding screen.', now() - interval '12 days', now()),
    (990011, 910001, 950004, 970002, 980002, 980002, 950002, now() - interval '7 days',  2, 'Built reactive forms and list filtering.', now() - interval '12 days', now()),
    (990012, 910001, 950004, 970005, 980001, NULL,   950002, now() - interval '5 days',  3, 'Needs clearer facilitation evidence before validation.', now() - interval '10 days', now()),
    (990013, 910001, 950005, 970004, 980003, 980003, 950001, now() - interval '13 days', 2, 'Designed analytics extracts for talent reporting.', now() - interval '17 days', now()),
    (990014, 910001, 950005, 970007, 980002, 980002, 950001, now() - interval '12 days', 2, 'Mapped data team skills into a heatmap.', now() - interval '17 days', now()),
    (990015, 910001, 950006, 970004, 980002, NULL,   NULL,   NULL,                    1, 'Built first PostgreSQL report for matrix coverage.', now() - interval '1 day', now()),
    (990016, 910001, 950006, 970003, 980002, 980002, 950005, now() - interval '6 days',  2, 'Designed API contract tests for analytics endpoints.', now() - interval '10 days', now()),
    (990101, 910002, 950101, 970101, 980102, 980102, 950101, now() - interval '8 days',  2, 'Automated operations handoff process.', now() - interval '12 days', now()),
    (990102, 910002, 950102, 970101, 980101, NULL,   NULL,   NULL,                    1, 'Documented process automation candidates.', now() - interval '3 days', now()),
    (990103, 910002, 950103, 970102, 980102, 980102, 950101, now() - interval '4 days',  2, 'Built weekly operations dashboard.', now() - interval '6 days', now());

-- 7) Phase 1.5 goal and learning rows. The current frontend uses mock data for
-- these pages, but these rows keep the backend schema testable through psql and
-- future API slices.
INSERT INTO goal (
    id, tenant_id, employee_id, author_employee_id, title, description, period,
    target_type, target_value, current_value, due_on, status, target_skill_id,
    created_on, updated_on
)
VALUES
    (991001, 910001, 950003, 950001, 'Reach Angular Practitioner', 'Validate Angular Delivery at practitioner level.', 0, 1, 2, 1, current_date + 45, 1, 970002, now() - interval '12 days', now()),
    (991002, 910001, 950004, 950002, 'Improve facilitation evidence', 'Run one team retro and upload clear evidence.', 0, 2, 1, 0, current_date + 30, 1, 970005, now() - interval '10 days', now()),
    (991003, 910001, 950006, 950005, 'Complete reporting basics', 'Finish two reporting resources and validate PostgreSQL level.', 0, 0, 2, 1, current_date + 60, 1, 970004, now() - interval '8 days', now());

INSERT INTO goal_check_in (
    id, tenant_id, goal_id, author_employee_id, note, progress_value, created_on, updated_on
)
VALUES
    (992001, 910001, 991001, 950003, 'Finished the profile page refactor and collected review notes.', 1, now() - interval '5 days', now()),
    (992002, 910001, 991003, 950006, 'Completed first SQL reporting exercise.', 1, now() - interval '3 days', now());

INSERT INTO learning_resource (
    id, tenant_id, title, description, type, url, media_file_id,
    estimated_minutes, points_awarded, is_active, created_on, updated_on
)
VALUES
    (993001, 910001, 'Angular Reactive Forms Deep Dive', 'Internal course for building reliable form-heavy product flows.', 2, 'https://learn.compoundyou.local/angular-forms', NULL, 90, 40, true, now(), now()),
    (993002, 910001, 'Skill Calibration Workshop Kit', 'Facilitation guide for running manager validation sessions.', 1, 'https://learn.compoundyou.local/calibration-kit', NULL, 35, 20, true, now(), now()),
    (993003, 910001, 'PostgreSQL Reporting Foundations', 'Hands-on reporting exercises for tenant-scoped skill analytics.', 2, 'https://learn.compoundyou.local/postgres-reporting', NULL, 120, 50, true, now(), now());

-- 8) Phase 3 career transparency: job families, levels, role profiles,
-- role requirements, employee assignments, team requirements and snapshots.
INSERT INTO job_family (id, tenant_id, name, description, is_active, created_on, updated_on)
VALUES
    (956001, 910001, 'Product Engineering', 'Engineering career ladder for product delivery roles.', true, now(), now()),
    (956002, 910001, 'Data & Insights', 'Analytics and reporting roles for evidence-based talent decisions.', true, now(), now()),
    (956003, 910001, 'People Enablement', 'Enablement and coaching roles around skills, goals and calibration.', true, now(), now()),
    (956101, 910002, 'Operations Excellence', 'Operational process and analytics career framework.', true, now(), now());

INSERT INTO career_level (
    id, tenant_id, job_family_id, "order", name, description, created_on, updated_on
)
VALUES
    (956011, 910001, 956001, 1, 'Associate', 'Contributes to scoped delivery with guidance.', now(), now()),
    (956012, 910001, 956001, 2, 'Professional', 'Owns features and resolves ambiguity in product delivery.', now(), now()),
    (956013, 910001, 956001, 3, 'Senior', 'Leads complex work and raises team engineering quality.', now(), now()),
    (956014, 910001, 956001, 4, 'Staff', 'Shapes multi-team architecture, practices and technical direction.', now(), now()),
    (956021, 910001, 956002, 1, 'Analyst', 'Builds foundational reports and interprets data with support.', now(), now()),
    (956022, 910001, 956002, 2, 'Engineer', 'Owns reporting models and reliable analytical delivery.', now(), now()),
    (956023, 910001, 956002, 3, 'Senior Engineer', 'Designs analytics systems and coaches data practitioners.', now(), now()),
    (956031, 910001, 956003, 1, 'Enablement Specialist', 'Runs enablement rituals and prepares calibration evidence.', now(), now()),
    (956032, 910001, 956003, 2, 'Enablement Lead', 'Owns manager enablement and cross-team calibration quality.', now(), now()),
    (956111, 910002, 956101, 1, 'Specialist', 'Documents and improves repeatable operating processes.', now(), now()),
    (956112, 910002, 956101, 2, 'Analyst', 'Builds metrics and automates operating workflows.', now(), now()),
    (956113, 910002, 956101, 3, 'Lead', 'Leads operational excellence across teams.', now(), now());

INSERT INTO role_profile (
    id, tenant_id, job_family_id, career_level_id, name, description, is_active, created_on, updated_on
)
VALUES
    (957001, 910001, 956001, 956011, 'Associate Frontend Engineer', 'Early-career frontend role focused on scoped UI delivery.', true, now(), now()),
    (957002, 910001, 956001, 956012, 'Frontend Engineer', 'Owns frontend product slices end-to-end.', true, now(), now()),
    (957003, 910001, 956001, 956013, 'Senior Frontend Engineer', 'Leads complex frontend initiatives and mentors peers.', true, now(), now()),
    (957004, 910001, 956001, 956014, 'Staff Frontend Engineer', 'Sets frontend architecture and capability standards.', true, now(), now()),
    (957005, 910001, 956002, 956022, 'Analytics Engineer', 'Builds tenant-scoped reporting models and data contracts.', true, now(), now()),
    (957006, 910001, 956002, 956023, 'Senior Analytics Engineer', 'Owns analytical architecture and talent reporting quality.', true, now(), now()),
    (957007, 910001, 956003, 956032, 'People Enablement Lead', 'Runs manager calibration and people growth systems.', true, now(), now()),
    (957101, 910002, 956101, 956111, 'Operations Specialist', 'Documents workflows and supports repeatable operations.', true, now(), now()),
    (957102, 910002, 956101, 956112, 'Operations Analyst', 'Automates workflows and builds operating metrics.', true, now(), now()),
    (957103, 910002, 956101, 956113, 'Operations Lead', 'Owns operational excellence and cross-team decision rhythm.', true, now(), now());

INSERT INTO role_profile_skill_requirement (
    id, tenant_id, role_profile_id, skill_id, required_skill_level_id, weight, created_on, updated_on
)
VALUES
    (958001, 910001, 957001, 970001, 980001, 1.00, now(), now()),
    (958002, 910001, 957001, 970002, 980001, 1.00, now(), now()),
    (958003, 910001, 957002, 970001, 980002, 1.50, now(), now()),
    (958004, 910001, 957002, 970002, 980002, 1.50, now(), now()),
    (958005, 910001, 957002, 970003, 980001, 1.00, now(), now()),
    (958006, 910001, 957003, 970001, 980003, 1.50, now(), now()),
    (958007, 910001, 957003, 970002, 980003, 1.50, now(), now()),
    (958008, 910001, 957003, 970003, 980002, 1.00, now(), now()),
    (958009, 910001, 957003, 970005, 980002, 0.75, now(), now()),
    (958010, 910001, 957004, 970001, 980003, 1.25, now(), now()),
    (958011, 910001, 957004, 970003, 980003, 1.00, now(), now()),
    (958012, 910001, 957004, 970006, 980002, 1.25, now(), now()),
    (958013, 910001, 957004, 970007, 980002, 1.00, now(), now()),
    (958014, 910001, 957005, 970004, 980002, 1.50, now(), now()),
    (958015, 910001, 957005, 970003, 980002, 1.00, now(), now()),
    (958016, 910001, 957006, 970004, 980003, 1.50, now(), now()),
    (958017, 910001, 957006, 970003, 980002, 1.00, now(), now()),
    (958018, 910001, 957006, 970007, 980002, 1.00, now(), now()),
    (958019, 910001, 957007, 970005, 980003, 1.50, now(), now()),
    (958020, 910001, 957007, 970006, 980002, 1.25, now(), now()),
    (958021, 910001, 957007, 970007, 980002, 1.00, now(), now()),
    (958101, 910002, 957101, 970101, 980101, 1.00, now(), now()),
    (958102, 910002, 957102, 970101, 980102, 1.25, now(), now()),
    (958103, 910002, 957102, 970102, 980102, 1.25, now(), now()),
    (958104, 910002, 957103, 970101, 980103, 1.25, now(), now()),
    (958105, 910002, 957103, 970102, 980103, 1.25, now(), now());

INSERT INTO employee_role_profile (
    id, tenant_id, employee_id, role_profile_id, assigned_on, is_active, created_on, updated_on
)
VALUES
    (958201, 910001, 950001, 957003, now() - interval '60 days', true, now(), now()),
    (958202, 910001, 950002, 957003, now() - interval '55 days', true, now(), now()),
    (958203, 910001, 950003, 957002, now() - interval '40 days', true, now(), now()),
    (958204, 910001, 950004, 957001, now() - interval '35 days', true, now(), now()),
    (958205, 910001, 950005, 957005, now() - interval '50 days', true, now(), now()),
    (958206, 910001, 950006, 957005, now() - interval '25 days', true, now(), now()),
    (958301, 910002, 950101, 957102, now() - interval '25 days', true, now(), now()),
    (958302, 910002, 950102, 957101, now() - interval '20 days', true, now(), now()),
    (958303, 910002, 950103, 957102, now() - interval '15 days', true, now(), now());

INSERT INTO team_skill_requirement (
    id, tenant_id, team_id, skill_id, required_skill_level_id, weight, created_on, updated_on
)
VALUES
    (958401, 910001, 930001, 970001, 980002, 2, now(), now()),
    (958402, 910001, 930001, 970002, 980002, 2, now(), now()),
    (958403, 910001, 930001, 970003, 980002, 1, now(), now()),
    (958404, 910001, 930001, 970005, 980001, 1, now(), now()),
    (958405, 910001, 930002, 970004, 980002, 2, now(), now()),
    (958406, 910001, 930002, 970003, 980002, 1, now(), now()),
    (958407, 910001, 930002, 970007, 980001, 1, now(), now()),
    (958408, 910001, 930003, 970005, 980002, 2, now(), now()),
    (958409, 910001, 930003, 970006, 980002, 2, now(), now()),
    (958501, 910002, 930101, 970101, 980102, 2, now(), now()),
    (958502, 910002, 930101, 970102, 980102, 2, now(), now());

INSERT INTO career_path_snapshot (
    id, tenant_id, employee_id, current_role_profile_id, target_role_profile_id,
    readiness_score, skill_fit_score, validation_coverage_score, goal_completion_score,
    band, scored_on, created_on, updated_on
)
VALUES
    (958601, 910001, 950003, 957002, 957003, 42, 38, 50, 50, 0, now() - interval '7 days', now() - interval '7 days', now()),
    (958602, 910001, 950004, 957001, 957002, 63, 59, 75, 40, 1, now() - interval '6 days', now() - interval '6 days', now()),
    (958603, 910001, 950005, 957005, 957006, 82, 88, 67, 60, 1, now() - interval '5 days', now() - interval '5 days', now()),
    (958604, 910002, 950102, 957101, 957102, 54, 50, 60, 30, 0, now() - interval '3 days', now() - interval '3 days', now()),
    (958605, 910002, 950103, 957102, 957103, 76, 72, 88, 50, 1, now() - interval '2 days', now() - interval '2 days', now());

-- 9) Audit rows for Settings / Audit page smoke tests.
INSERT INTO audit_log_entry (
    id, tenant_id, actor_user_id, action, entity_type, entity_id,
    occurred_on, metadata_json
)
VALUES
    (994001, 910001, 900001, 'tenant.create', 'Tenant', 910001, now() - interval '120 days', '{"fixture":true,"screen":"settings"}'),
    (994002, 910001, 900001, 'department.create', 'Department', 920001, now() - interval '110 days', '{"fixture":true,"name":"Product & Engineering"}'),
    (994003, 910001, 900001, 'team.create', 'Team', 930001, now() - interval '100 days', '{"fixture":true,"name":"Frontend Platform"}'),
    (994004, 910001, 900002, 'employee_skill.submit_assessment', 'EmployeeSkillAssessment', 990006, now() - interval '3 days', '{"fixture":true,"status":"PendingValidation"}'),
    (994005, 910001, 900001, 'employee_skill.validate_assessment', 'EmployeeSkillAssessment', 990004, now() - interval '12 days', '{"fixture":true,"status":"Validated"}'),
    (994006, 910001, 900001, 'role-profile-skill-requirements.bulk-set', 'RoleProfile', 957003, now() - interval '9 days', '{"fixture":true,"phase":3}'),
    (994007, 910001, 900001, 'employee-role-profile.assign', 'EmployeeRoleProfile', 958203, now() - interval '8 days', '{"fixture":true,"employeeId":950003}'),
    (994008, 910001, 900002, 'career-path.snapshot.create', 'CareerPathSnapshot', 958601, now() - interval '7 days', '{"fixture":true,"readinessScore":42}'),
    (994101, 910002, 900001, 'tenant.switch', 'TenantMembership', 950101, now() - interval '7 days', '{"fixture":true,"tenant":"globex-industries"}'),
    (994102, 910002, 900001, 'team-skill-requirements.bulk-set', 'Team', 930101, now() - interval '4 days', '{"fixture":true,"phase":3}');

-- 10) Keep identity sequences ahead of explicit fixture IDs.
SELECT setval(pg_get_serial_sequence('public."user"', 'id'),                     COALESCE((SELECT MAX(id) FROM "user"), 1), true);
SELECT setval(pg_get_serial_sequence('public.tenant', 'id'),                     COALESCE((SELECT MAX(id) FROM tenant), 1), true);
SELECT setval(pg_get_serial_sequence('public.tenant_membership', 'id'),          COALESCE((SELECT MAX(id) FROM tenant_membership), 1), true);
SELECT setval(pg_get_serial_sequence('public.tenant_invitation', 'id'),          COALESCE((SELECT MAX(id) FROM tenant_invitation), 1), true);
SELECT setval(pg_get_serial_sequence('public.department', 'id'),                 COALESCE((SELECT MAX(id) FROM department), 1), true);
SELECT setval(pg_get_serial_sequence('public.team', 'id'),                       COALESCE((SELECT MAX(id) FROM team), 1), true);
SELECT setval(pg_get_serial_sequence('public.employee', 'id'),                   COALESCE((SELECT MAX(id) FROM employee), 1), true);
SELECT setval(pg_get_serial_sequence('public.skill_category', 'id'),             COALESCE((SELECT MAX(id) FROM skill_category), 1), true);
SELECT setval(pg_get_serial_sequence('public.skill', 'id'),                      COALESCE((SELECT MAX(id) FROM skill), 1), true);
SELECT setval(pg_get_serial_sequence('public.skill_level', 'id'),                COALESCE((SELECT MAX(id) FROM skill_level), 1), true);
SELECT setval(pg_get_serial_sequence('public.employee_skill_assessment', 'id'),  COALESCE((SELECT MAX(id) FROM employee_skill_assessment), 1), true);
SELECT setval(pg_get_serial_sequence('public.goal', 'id'),                       COALESCE((SELECT MAX(id) FROM goal), 1), true);
SELECT setval(pg_get_serial_sequence('public.goal_check_in', 'id'),              COALESCE((SELECT MAX(id) FROM goal_check_in), 1), true);
SELECT setval(pg_get_serial_sequence('public.learning_resource', 'id'),          COALESCE((SELECT MAX(id) FROM learning_resource), 1), true);
SELECT setval(pg_get_serial_sequence('public.job_family', 'id'),                 COALESCE((SELECT MAX(id) FROM job_family), 1), true);
SELECT setval(pg_get_serial_sequence('public.career_level', 'id'),               COALESCE((SELECT MAX(id) FROM career_level), 1), true);
SELECT setval(pg_get_serial_sequence('public.role_profile', 'id'),               COALESCE((SELECT MAX(id) FROM role_profile), 1), true);
SELECT setval(pg_get_serial_sequence('public.role_profile_skill_requirement', 'id'), COALESCE((SELECT MAX(id) FROM role_profile_skill_requirement), 1), true);
SELECT setval(pg_get_serial_sequence('public.employee_role_profile', 'id'),      COALESCE((SELECT MAX(id) FROM employee_role_profile), 1), true);
SELECT setval(pg_get_serial_sequence('public.team_skill_requirement', 'id'),     COALESCE((SELECT MAX(id) FROM team_skill_requirement), 1), true);
SELECT setval(pg_get_serial_sequence('public.career_path_snapshot', 'id'),       COALESCE((SELECT MAX(id) FROM career_path_snapshot), 1), true);
SELECT setval(pg_get_serial_sequence('public.audit_log_entry', 'id'),            COALESCE((SELECT MAX(id) FROM audit_log_entry), 1), true);

COMMIT;

-- 11) Verify.
SELECT u.id AS user_id,
       u.email,
       t.id AS tenant_id,
       t.slug,
       m.id AS membership_id,
       e.id AS employee_id,
       m.role AS role_int
FROM "user" u
JOIN tenant_membership m ON m.user_id = u.id
JOIN tenant t            ON t.id      = m.tenant_id
LEFT JOIN employee e     ON e.tenant_id = t.id AND e.user_id = u.id
WHERE u.email IN (
    'test@compoundyou.local',
    'maya.manager@compoundyou.local',
    'jonas.employee@compoundyou.local',
    'zoe.ops@compoundyou.local'
)
ORDER BY t.slug, u.email;

SELECT t.slug,
       COUNT(DISTINCT d.id) AS departments,
       COUNT(DISTINCT team.id) AS teams,
       COUNT(DISTINCT e.id) AS employees,
       COUNT(DISTINCT s.id) AS skills,
       COUNT(DISTINCT a.id) AS assessments,
       COUNT(DISTINCT jf.id) AS job_families,
       COUNT(DISTINCT rp.id) AS role_profiles,
       COUNT(DISTINCT erp.id) AS role_assignments,
       COUNT(DISTINCT tsr.id) AS team_requirements
FROM tenant t
LEFT JOIN department d ON d.tenant_id = t.id
LEFT JOIN team ON team.tenant_id = t.id
LEFT JOIN employee e ON e.tenant_id = t.id
LEFT JOIN skill s ON s.tenant_id = t.id
LEFT JOIN employee_skill_assessment a ON a.tenant_id = t.id
LEFT JOIN job_family jf ON jf.tenant_id = t.id
LEFT JOIN role_profile rp ON rp.tenant_id = t.id
LEFT JOIN employee_role_profile erp ON erp.tenant_id = t.id
LEFT JOIN team_skill_requirement tsr ON tsr.tenant_id = t.id
WHERE t.slug IN ('acme-corp', 'globex-industries')
GROUP BY t.slug
ORDER BY t.slug;
