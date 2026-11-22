\set ON_ERROR_STOP on
BEGIN;
INSERT INTO role_permissions VALUES ('operator', 'shipment.write'), ('manager', 'shipment.write');

ALTER TABLE approved_consignees ADD UNIQUE (id, tenant_id, branch_id);
CREATE TABLE shipment_drafts (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    version integer NOT NULL DEFAULT 1 CHECK (version > 0),
    status text NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'validated')),
    consignee_id uuid,
    origin text REFERENCES airports,
    destination text REFERENCES airports,
    gross_weight_kg numeric CHECK (gross_weight_kg > 0),
    FOREIGN KEY (branch_id, tenant_id) REFERENCES branches (id, tenant_id),
    FOREIGN KEY (consignee_id, tenant_id, branch_id)
        REFERENCES approved_consignees (id, tenant_id, branch_id),
    CHECK (status = 'draft' OR (consignee_id IS NOT NULL AND origin IS NOT NULL
        AND destination IS NOT NULL AND origin <> destination AND gross_weight_kg IS NOT NULL))
);
ALTER TABLE shipment_drafts ENABLE ROW LEVEL SECURITY;
CREATE POLICY shipment_read ON shipment_drafts FOR SELECT TO :"runtime_role"
    USING (has_m1_permission('shipment.write') AND EXISTS (
        SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = shipment_drafts.tenant_id));
CREATE POLICY shipment_create ON shipment_drafts FOR INSERT TO :"runtime_role"
    WITH CHECK (status = 'draft' AND has_m1_permission('shipment.write') AND EXISTS (
        SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = shipment_drafts.tenant_id));
CREATE POLICY shipment_update ON shipment_drafts FOR UPDATE TO :"runtime_role"
    USING (status = 'draft' AND has_m1_permission('shipment.write') AND EXISTS (
        SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = shipment_drafts.tenant_id))
    WITH CHECK (has_m1_permission('shipment.write') AND EXISTS (
        SELECT 1 FROM branches b WHERE b.id = branch_id AND b.tenant_id = shipment_drafts.tenant_id));

ALTER TABLE audit_events DROP CONSTRAINT audit_events_action_check;
ALTER TABLE audit_events ADD CHECK (action IN
    ('party.requested', 'party.approved', 'party.rejected', 'shipment.validated'));
CREATE POLICY shipment_audit_append ON audit_events FOR INSERT TO :"runtime_role"
    WITH CHECK (action = 'shipment.validated' AND actor_id IN (SELECT id FROM users)
        AND has_m1_permission('shipment.write') AND EXISTS (
            SELECT 1 FROM shipment_drafts s WHERE s.id = entity_id AND s.status = 'validated'
                AND s.tenant_id = audit_events.tenant_id AND s.branch_id = audit_events.branch_id));
GRANT SELECT, INSERT ON shipment_drafts TO :"runtime_role";
GRANT UPDATE (version, status, consignee_id, origin, destination, gross_weight_kg)
    ON shipment_drafts TO :"runtime_role";
COMMIT;
