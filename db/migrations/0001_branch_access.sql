-- Apply once as the schema owner. Supply a separate, unprivileged runtime role.
\set ON_ERROR_STOP on
BEGIN;

CREATE TABLE tenants (
    id uuid PRIMARY KEY,
    name text NOT NULL,
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


ALTER TABLE users ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE membership_branches ENABLE ROW LEVEL SECURITY;
ALTER TABLE branches ENABLE ROW LEVEL SECURITY;

CREATE POLICY users_read ON users FOR SELECT TO :"runtime_role"
    USING (is_active
        AND identity_issuer = current_setting('tms.identity_issuer', true)
        AND identity_subject = current_setting('tms.identity_subject', true));

CREATE POLICY memberships_read ON memberships FOR SELECT TO :"runtime_role"
    USING (is_active
        AND tenant_id = nullif(current_setting('tms.tenant_id', true), '')::uuid
        AND EXISTS (SELECT 1 FROM users u WHERE u.id = memberships.user_id));

CREATE POLICY tenants_read ON tenants FOR SELECT TO :"runtime_role"
    USING (is_active AND EXISTS (
        SELECT 1 FROM memberships m WHERE m.tenant_id = tenants.id
    ));

CREATE POLICY membership_branches_read ON membership_branches FOR SELECT TO :"runtime_role"
    USING (EXISTS (
        SELECT 1 FROM memberships m
        WHERE m.id = membership_branches.membership_id
          AND m.tenant_id = membership_branches.tenant_id
    ));

CREATE POLICY branches_read ON branches FOR SELECT TO :"runtime_role"
    USING (is_active AND EXISTS (
        SELECT 1 FROM memberships m
        JOIN tenants t ON t.id = m.tenant_id
        WHERE m.tenant_id = branches.tenant_id
          AND (m.all_branches OR EXISTS (
              SELECT 1 FROM membership_branches mb
              WHERE mb.membership_id = m.id
                AND mb.tenant_id = branches.tenant_id
                AND mb.branch_id = branches.id
          ))
    ));

GRANT USAGE ON SCHEMA public TO :"runtime_role";
GRANT SELECT ON tenants, users, branches, memberships, membership_branches TO :"runtime_role";
COMMIT;
