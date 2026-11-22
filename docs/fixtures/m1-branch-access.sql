-- Load only into a disposable, empty development database.
-- This is the minimum schema for exercising the branch lookup.
\set ON_ERROR_STOP on
BEGIN;

CREATE TABLE tenants (
    id uuid PRIMARY KEY,
    is_active boolean NOT NULL DEFAULT true
);
CREATE TABLE users (
    id uuid PRIMARY KEY,
    identity_issuer text NOT NULL,
    identity_subject text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (identity_issuer, identity_subject)
);
CREATE TABLE branches (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants,
    code text NOT NULL,
    name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (id, tenant_id),
    UNIQUE (tenant_id, code)
);
CREATE TABLE memberships (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users,
    tenant_id uuid NOT NULL REFERENCES tenants,
    all_branches boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (user_id, tenant_id),
    UNIQUE (id, tenant_id)
);
CREATE TABLE membership_branches (
    membership_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    PRIMARY KEY (membership_id, branch_id),
    FOREIGN KEY (membership_id, tenant_id) REFERENCES memberships (id, tenant_id),
    FOREIGN KEY (branch_id, tenant_id) REFERENCES branches (id, tenant_id)
);

INSERT INTO tenants (id) VALUES
    ('10000000-0000-0000-0000-000000000001'),
    ('10000000-0000-0000-0000-000000000002');
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
