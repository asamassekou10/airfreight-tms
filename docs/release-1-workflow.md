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

## Review before implementation

- Operations reviewer: Confirm required fields, booking and AWB ordering, cancellation rules, milestone evidence, and what billing tracks per house versus per consolidation.
- Both developers: Agree the permission matrix and branch scope with the tenancy design in issue #3. Record decisions before using this draft as an implementation contract.
