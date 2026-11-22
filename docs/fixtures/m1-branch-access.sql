-- Load only into a disposable, empty development database.
-- Pass a separate runtime role with psql -v runtime_role=...
\set ON_ERROR_STOP on
\ir ../../db/migrations/0001_branch_access.sql
\ir ../../db/migrations/0002_m1_reference_catalogs.sql
BEGIN;

INSERT INTO tenants (id, name) VALUES
    ('10000000-0000-0000-0000-000000000001', 'Demo Forwarding'),
    ('10000000-0000-0000-0000-000000000002', 'Other Forwarding');
INSERT INTO users (id, identity_issuer, identity_subject) VALUES
    ('20000000-0000-0000-0000-000000000001', 'https://identity.example.invalid', 'demo-operator');
INSERT INTO branches (id, tenant_id, code, name) VALUES
    ('30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'CHI', 'Chicago'),
    ('30000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'DAL', 'Dallas'),
    ('30000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000002', 'FRA', 'Frankfurt');
INSERT INTO memberships (id, user_id, tenant_id) VALUES
    ('40000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001');
INSERT INTO membership_branches (membership_id, branch_id, tenant_id) VALUES
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001');

COMMIT;
