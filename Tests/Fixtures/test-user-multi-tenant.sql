-- Test fixture: a user with two tenant memberships, ready to exercise the
-- multi-tenant login picker. Idempotent -- re-run any time to reset state
-- (including the OTP code) without leftovers from prior runs.
--
-- Usage (from a psql shell against the dev database):
--   \i Tests/Fixtures/test-user-multi-tenant.sql
--
-- Login flow against the running backend:
--   1. PUT /api/users/login
--        body: { "email": "test@compoundyou.local", "code": "123456" }
--      (skip the RequestLogin step -- the OTP is pre-set below)
--   2. Response: { token, requiresTenantSelection: true, availableTenants: [...] }
--   3. POST /api/users/switch-tenant
--        body: { "tenantId": <one of the ids returned in step 2> }
--   4. Response: tenant-bound token.

BEGIN;

-- 1) Wipe prior state. FK cascades from tenant -> memberships/departments/
--    teams/employees/invitations clear child rows automatically.
DELETE FROM tenant WHERE slug IN ('acme-corp', 'globex-industries');
DELETE FROM "user" WHERE email = 'test@compoundyou.local';

-- 2) Create user + two tenants + two memberships in a single statement.
WITH new_user AS (
    INSERT INTO "user" (
        display_name, email, phone_number,
        sign_in_secret, sign_in_tries, is_platform_admin,
        created_on, updated_on
    )
    VALUES (
        'Test User', 'test@compoundyou.local', NULL,
        '123456', 3, false,
        now(), now()
    )
    RETURNING id
),
acme AS (
    INSERT INTO tenant (name, slug, status, plan, owner_user_id, created_on, updated_on)
    SELECT 'Acme Corp', 'acme-corp', 0, 'Pro', new_user.id, now(), now()
    FROM new_user
    RETURNING id
),
globex AS (
    INSERT INTO tenant (name, slug, status, plan, owner_user_id, created_on, updated_on)
    SELECT 'Globex Industries', 'globex-industries', 0, 'Pro', new_user.id, now(), now()
    FROM new_user
    RETURNING id
)
INSERT INTO tenant_membership (
    tenant_id, user_id, role, joined_on, is_active, created_on, updated_on
)
SELECT acme.id,   new_user.id, 2, now(), true, now(), now() FROM new_user, acme   -- 2 = TenantAdmin
UNION ALL
SELECT globex.id, new_user.id, 1, now(), true, now(), now() FROM new_user, globex; -- 1 = Manager

COMMIT;

-- 3) Verify
SELECT u.id   AS user_id,
       u.email,
       t.id   AS tenant_id,
       t.slug,
       m.role AS role_int
FROM "user" u
JOIN tenant_membership m ON m.user_id = u.id
JOIN tenant t            ON t.id      = m.tenant_id
WHERE u.email = 'test@compoundyou.local'
ORDER BY t.slug;
