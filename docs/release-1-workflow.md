# Release 1 workflow and permissions

Draft for operations and engineering review. [Issue #1](https://github.com/asamassekou10/airfreight-tms/issues/1)

## Shipment lifecycle

| From | To | Who | Conditions |
| --- | --- | --- | --- |
| None | Draft | Operator, Manager | Active branch in the user's tenant. Allow incomplete drafts. |
| Draft | Validated | Operator, Manager | Required parties are approved and eligible. Route, cargo, and required commercial fields pass validation. Return field errors and retain Draft on failure. |
| Validated | Draft | Operator, Manager | Reopen before booking to change validated details. Require validation again. |
| Validated | Booked | Operator, Manager | Record a confirmed booking reference. For a consolidation, use the shared booking. |
| Booked | In transit | Operator, Manager | Record departure for the applicable air movement. |
| In transit | Arrived | Operator, Manager | Record arrival at the final airport. |
| Arrived | Delivered | Operator, Manager | Record delivery or approved handover evidence. |
| Draft, Validated, Booked | Cancelled | Manager | Require a reason. If booked, reconcile the booking and any issued AWB before completing cancellation. |

Statuses describe the shipment. Milestones are timestamped events with a source and may be recorded per leg. Duplicate events must not advance state twice. Late or conflicting events should be flagged without moving the shipment backward automatically. Confirm exception handling for partial movements, rebooking, and cancellation after departure with operations.

## Straight and consolidated paths

- **Straight:** Create and validate a shipment, confirm booking, then issue the applicable master AWB. Record departure, arrival, and delivery. Finance records costs, revenue, and invoice status separately from shipment status.
- **Consolidated:** Validate each house shipment, group eligible houses under one consolidation and master AWB, and confirm the shared booking. Each house has its own house AWB. Shared flight milestones may apply to multiple houses, while delivery and billing are tracked per house. Confirm grouping rules, issuance order, and when membership becomes locked.
- **AWB lifecycle proposal:** Draft → Issued → Superseded or Voided. Issuance requires validated data, an eligible booking, and number allocation. Corrections create a new issued version and preserve the prior version. Voiding requires a reason and retains history. Partner send, acceptance, and rejection are recorded separately from document state. Confirm which edits require revalidation or partner resubmission.

## Proposed permissions

All grants apply within the user's tenant and authorized branches where the resource has branch scope. The API derives scope from active membership and denies actions without an explicit grant. Tenant administration and operational authority are separate grants.

| Action | Admin | Manager | Operator | Finance |
| --- | --- | --- | --- | --- |
| Manage tenant memberships and branch access | Yes | — | — | — |
| Read approved party data and shipments | — | Yes | Yes | Shipment details needed for billing |
| Request new or changed party data | — | Yes | Yes | — |
| Approve, reject, or archive party data | — | Yes | — | — |
| Create, edit, validate, or reopen drafts | — | Yes | Yes | — |
| Record booking and operational milestones | — | Yes | Yes | — |
| Cancel shipment or issue, correct, or void AWB | — | Yes | — | — |
| View cost, revenue, and margin | — | — | — | Yes |
| Record costs, revenue, and invoice status | — | — | — | Yes |
| View audit history | Membership and branch administration | Operations in scope | — | — |

Confirm role combinations, who may grant Admin, how manager delegation works, whether managers can approve their own party requests, and whether managers need margin visibility. Confirm whether all-branch grants include future branches and which operations affect tenant-wide party records. A branch-scoped grant with no eligible branches provides no branch access.

## M1 acceptance walkthrough

1. An operator finds that the consignee is missing and submits a party request. The pending record cannot be selected for validation.
2. An authorized manager approves the request with a reason. The approved record becomes selectable. Rejected or archived records cannot be newly selected.
3. The operator creates a straight shipment draft. Validation reports missing required data without changing state. After correcting the draft, validation advances it to Validated.
4. The same operations are denied for unauthorized roles and across tenant or branch boundaries. Audit history records the actor, action, affected record, and outcome of successful business changes.

M1 ends at draft validation. Booking, consolidation, AWB issuance, partner exchange, delivery, and finance follow in later slices. Native desktop, customs filing, and general ledger are outside Release 1.

## Concrete M1 review scenario

Use synthetic data throughout. Demo Forwarding has two branches, Chicago and Dallas. A Chicago operator and a Chicago manager each have access only to Chicago. Another forwarding company has its own operator and manager. The sample shipment is a straight shipment from ORD to FRA with two cartons of printed brochures at 24 kg total gross weight.

| Step | Action | Expected result |
| --- | --- | --- |
| 1 | Chicago operator requests the missing consignee, Demo Receiving GmbH. | The request is pending and the consignee is unavailable for shipment validation. |
| 2 | The operator attempts to approve the request. | The API denies approval and the request remains pending. |
| 3 | The Chicago manager approves the request with a reason. | An approved consignee becomes selectable and the approval is recorded in the audit history. |
| 4 | The Chicago operator saves the draft with the approved consignee but leaves gross weight blank, then submits it for validation. | The draft saves. Validation identifies the missing gross weight and the shipment remains Draft. |
| 5 | The operator adds 24 kg and submits again with all other required fields present. | The shipment advances to Validated and the transition is recorded in the audit history. |
| 6 | A Dallas-only user or a user from the other tenant attempts to read or validate the Chicago shipment. | The API denies access, exposes no shipment data, and leaves the record unchanged. |

### Decisions to record during the walkthrough

| Topic | Proposal for review |
| --- | --- |
| Required fields | Confirm whether customer, shipper, consignee, origin and destination airports, piece count, gross weight, goods description, and Incoterm with named place are sufficient for M1. Decide when a notify party is mandatory. |
| Party approval scope | Decide whether the Chicago manager may approve a request that creates a party usable by Dallas. If approval is tenant-wide, define the grant that authorizes it. |
| Role combinations | Decide whether the same membership can act as both Operator and Manager, and whether the requester may approve their own request regardless of assigned roles. |
| Audit review | Confirm who can see party approval and draft validation events, and whether audit visibility follows branch scope, tenant scope, or both depending on the event. |

## Implementation criteria for the shipment draft

These criteria elaborate [issue #5](https://github.com/asamassekou10/airfreight-tms/issues/5). The exact required fields and role grants remain subject to the decisions above.

| Case | Expected API behavior |
| --- | --- |
| Save an incomplete draft | Persist supplied fields and keep the shipment in Draft. Reject malformed values and invalid references during the save. |
| Validate an incomplete draft | Return field-level errors and leave the state and version unchanged. |
| Validate with an ineligible party | Reject validation if a selected party is missing, unapproved, archived, outside the tenant, or lacks the required role. Apply the same checks when validating a previously saved reference. |
| Validate a complete draft | Persist the Validated state and its audit event in one transaction. The event records actor, shipment, branch, timestamp, and the state transition. |
| Save or validate an outdated version | Reject the request as a conflict. Preserve the current data and let the client reload before retrying. |
| List, read, edit, or validate outside authorized scope | Apply tenant and branch scope to each operation. Exclude inaccessible rows from lists and avoid disclosing record details through errors. |

For the first demonstration, show only the actions needed to find approved parties, save a draft, correct validation errors, and confirm the resulting status and audit record. Use the authorization foundation from issue #3 and approved party records from issue #4.

## Review before implementation

- Operations reviewer: Confirm required fields, booking and AWB ordering, cancellation rules, milestone evidence, and what billing tracks per house versus per consolidation.
- Both developers: Agree the permission matrix and branch scope with the tenancy design in issue #3. Record decisions before using this draft as an implementation contract.
