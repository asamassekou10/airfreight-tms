-- Load only into a disposable, empty development database.
-- Pass a separate runtime role with psql -v runtime_role=...
\set ON_ERROR_STOP on
\ir ../../db/migrations/0001_branch_access.sql
\ir ../../db/migrations/0002_m1_reference_catalogs.sql
\ir ../../db/migrations/0003_m1_party_approval.sql
\ir ../../db/migrations/0004_m1_shipments.sql
BEGIN;

INSERT INTO tenants (id, name) VALUES
    ('10000000-0000-0000-0000-000000000001', 'Demo Forwarding'),
    ('10000000-0000-0000-0000-000000000002', 'Other Forwarding');
INSERT INTO users (id, identity_issuer, identity_subject) VALUES
    ('20000000-0000-0000-0000-000000000001', 'https://identity.example.invalid', 'demo-operator'),
    ('20000000-0000-0000-0000-000000000002', 'https://identity.example.invalid', 'demo-manager');
INSERT INTO branches (id, tenant_id, code, name) VALUES
    ('30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'CHI', 'Chicago'),
    ('30000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'DAL', 'Dallas'),
    ('30000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000002', 'FRA', 'Frankfurt');
INSERT INTO memberships (id, user_id, tenant_id, role_code) VALUES
    ('40000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'operator'),
    ('40000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'manager');
INSERT INTO membership_branches (membership_id, branch_id, tenant_id) VALUES
    ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001'),
    ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001');

COMMIT;
