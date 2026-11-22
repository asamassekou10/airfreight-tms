\set ON_ERROR_STOP on
BEGIN;

CREATE TABLE roles (code text PRIMARY KEY);
INSERT INTO roles VALUES ('admin'), ('manager'), ('operator'), ('finance');
ALTER TABLE memberships ADD COLUMN role_code text REFERENCES roles;
CREATE TABLE role_permissions (
    role_code text REFERENCES roles,
    permission text NOT NULL,
    PRIMARY KEY (role_code, permission)
);
INSERT INTO role_permissions VALUES
    ('operator', 'party.request'),
    ('manager', 'party.request'),
    ('manager', 'party.approve');

CREATE FUNCTION has_m1_permission(required_permission text)
RETURNS boolean LANGUAGE sql STABLE SECURITY INVOKER AS $$
    SELECT EXISTS (
        SELECT 1 FROM public.memberships m
        JOIN public.role_permissions p ON p.role_code = m.role_code
        WHERE p.permission = required_permission
    )
$$;

CREATE TABLE party_requests (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    requester_id uuid NOT NULL REFERENCES users,
    name text NOT NULL CHECK (length(btrim(name)) BETWEEN 1 AND 200),
    status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
    FOREIGN KEY (branch_id, tenant_id) REFERENCES branches (id, tenant_id),
    UNIQUE (id, tenant_id, branch_id)
);

CREATE TABLE approved_consignees (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    name text NOT NULL,
    FOREIGN KEY (id, tenant_id, branch_id) REFERENCES party_requests (id, tenant_id, branch_id)
);
CREATE UNIQUE INDEX approved_consignee_name
    ON approved_consignees (tenant_id, branch_id, lower(btrim(name)));

CREATE TABLE audit_events (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    actor_id uuid NOT NULL REFERENCES users,
    action text NOT NULL CHECK (action IN ('party.requested', 'party.approved', 'party.rejected')),
    entity_id uuid NOT NULL,
    reason text,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (branch_id, tenant_id) REFERENCES branches (id, tenant_id)
);

ALTER TABLE party_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE approved_consignees ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY party_requests_read ON party_requests FOR SELECT TO :"runtime_role"
    USING (EXISTS (SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = party_requests.tenant_id)
        AND (requester_id IN (SELECT id FROM users) OR has_m1_permission('party.approve')));
CREATE POLICY party_requests_create ON party_requests FOR INSERT TO :"runtime_role"
    WITH CHECK (status = 'pending' AND requester_id IN (SELECT id FROM users)
        AND has_m1_permission('party.request')
        AND EXISTS (SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = party_requests.tenant_id));
CREATE POLICY party_requests_approve ON party_requests FOR UPDATE TO :"runtime_role"
    USING (status = 'pending' AND requester_id NOT IN (SELECT id FROM users)
        AND has_m1_permission('party.approve')
        AND EXISTS (SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = party_requests.tenant_id))
    WITH CHECK (status IN ('approved', 'rejected') AND has_m1_permission('party.approve')
        AND EXISTS (SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = party_requests.tenant_id));

CREATE POLICY approved_consignees_read ON approved_consignees FOR SELECT TO :"runtime_role"
    USING (EXISTS (SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = approved_consignees.tenant_id));
CREATE POLICY approved_consignees_create ON approved_consignees FOR INSERT TO :"runtime_role"
    WITH CHECK (has_m1_permission('party.approve') AND EXISTS (
        SELECT 1 FROM party_requests r WHERE r.id = approved_consignees.id
            AND r.tenant_id = approved_consignees.tenant_id AND r.branch_id = approved_consignees.branch_id
            AND r.status = 'approved' AND r.name = approved_consignees.name
    ));

CREATE POLICY audit_events_append ON audit_events FOR INSERT TO :"runtime_role"
    WITH CHECK (actor_id IN (SELECT id FROM users)
        AND EXISTS (SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = audit_events.tenant_id)
        AND CASE action WHEN 'party.requested' THEN has_m1_permission('party.request')
                        WHEN 'party.approved' THEN has_m1_permission('party.approve')
                        WHEN 'party.rejected' THEN has_m1_permission('party.approve') ELSE false END);
CREATE POLICY audit_events_read ON audit_events FOR SELECT TO :"runtime_role"
    USING (has_m1_permission('party.approve')
        AND EXISTS (SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = audit_events.tenant_id));

GRANT SELECT ON role_permissions, party_requests, approved_consignees, audit_events TO :"runtime_role";
GRANT INSERT ON party_requests, approved_consignees, audit_events TO :"runtime_role";
GRANT UPDATE (status) ON party_requests TO :"runtime_role";
COMMIT;
