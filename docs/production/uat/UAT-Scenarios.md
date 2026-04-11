# UAT Scenarios — DARAH ECM + xECM Platform
# دارة الملك عبدالعزيز — سيناريوهات قبول المستخدم
# Version: 1.0 | Environment: UAT | Date: April 2026

## Roles & Participants
| Role | User |
|------|------|
| Content Manager | uat-content@darah.gov.sa |
| Department Head | uat-head@darah.gov.sa |
| Records Manager | uat-records@darah.gov.sa |
| Legal Officer | uat-legal@darah.gov.sa |
| IT Administrator | uat-admin@darah.gov.sa |
| Basic User | uat-basic@darah.gov.sa |

---

## SCENARIO 1: Document Upload → Workflow Approval → Archival

**Pre-Conditions:** Library "مكتبة العقود" + WorkflowDefinition "اعتماد العقود" (2 steps) + RetentionPolicy "7 سنوات"

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | Login as Content Manager | Dashboard visible | ☐ |
| 2 | POST /api/v1/documents (TitleAr="عقد خدمات 2026", DocType=عقد) | 201 Status=DRAFT Version=1.0 | ☐ |
| 3 | POST /api/v1/workflow/submit/{docId} | DocStatus=PENDING, task created | ☐ |
| 4 | Login as Dept Head, GET /api/v1/workflow/inbox | Task visible + SLA timer | ☐ |
| 5 | POST /tasks/{taskId}/approve {"Comment":"موافق"} | DocStatus=APPROVED | ☐ |
| 6 | Verify Content Manager notification received | "وثيقتك معتمدة" within 2min | ☐ |
| 7 | POST /api/v1/records/{docId}/declare {RecordClassId, RetentionPolicyId} | ExpiryDate = today+7years | ☐ |
| 8 | GET /api/v1/audit/logs?entityId={docId} | Min 5 events with CorrelationId | ☐ |

**Pass Criteria:** DRAFT→PENDING→APPROVED, retention computed, audit complete, notification delivered

---

## SCENARIO 2: Version Control (CheckOut / CheckIn / History)

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | POST /documents/{docId}/checkout | IsCheckedOut=true | ☐ |
| 2 | Second user: POST /checkout same doc | 400 "الوثيقة محجوزة" | ☐ |
| 3 | Original user: POST /checkin (new file) | Version=1.1, unlocked | ☐ |
| 4 | GET /documents/{docId}/versions | Both 1.0 and 1.1 visible | ☐ |
| 5 | GET /versions/compare?v1=ID1&v2=ID2 | SameFile=false, delta shown | ☐ |
| 6 | POST /checkin MajorBump=true | Version=2.0 | ☐ |

**Pass Criteria:** Concurrent checkout blocked, history preserved, major bump correct

---

## SCENARIO 3: Classification Level Access Control

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | Upload doc ClassificationLevelOrder=4 (SECRET) | Document created | ☐ |
| 2 | Basic User: GET /documents/{docId} | 403 "مستوى التصنيف يتطلب صلاحية خاصة" | ☐ |
| 3 | Basic User: GET /documents/{docId}/download | 403 Forbidden | ☐ |
| 4 | Legal Officer (has secret permission): GET /documents/{docId} | 200 OK | ☐ |
| 5 | Verify: zero information leaked in 403 response | No document title/content in error body | ☐ |
| 6 | GET /audit/logs — denied attempts logged | Both denied + granted access audited | ☐ |

**Pass Criteria:** SECRET inaccessible, zero info leakage, all access attempts audited

---

## SCENARIO 4: Legal Hold — Workspace Cascade

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | Create workspace + bind 3 documents | DocumentCount=3 | ☐ |
| 2 | POST /workspaces/{wsId}/legal-hold | IsLegalHold=true, DocumentsAffected=3 | ☐ |
| 3 | GET /documents/{docId1} | IsLegalHold=true | ☐ |
| 4 | POST /documents/{docId1}/checkout | 400 "خاضعة لتجميد قانوني" | ☐ |
| 5 | DELETE /documents/{docId1} | 400 Blocked | ☐ |
| 6 | POST /records/disposal-requests {docId1} | 400 "يوجد وثائق خاضعة لتجميد قانوني" | ☐ |
| 7 | DELETE /workspaces/{wsId}/legal-hold | Released | ☐ |
| 8 | GET /documents/{docId1} | IsLegalHold=false | ☐ |

**Pass Criteria:** Cascade to ALL documents, blocks checkout/delete/disposal, release restores operations

---

## SCENARIO 5: SLA Breach and Escalation

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | Submit document (workflow SLA=1hr) | DueAt=now+1hr | ☐ |
| 2 | Advance test clock 1hr, trigger SLA check | — | ☐ |
| 3 | GET /workflow/inbox | IsOverdue=true, red indicator | ☐ |
| 4 | Verify email "انتهت مهلة المهمة" received | Email in inbox | ☐ |
| 5 | GET /workflow/inbox/summary | TotalOverdue > 0 | ☐ |
| 6 | GET /audit/logs?eventType=SLABreached | Entry with Severity=Warning | ☐ |

**Pass Criteria:** Breach detected within 15min, email sent, dashboard updated, audited

---

## SCENARIO 6: Records Disposal Workflow

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | Identify 3 docs: RetentionExpired + no LegalHold | Doc list | ☐ |
| 2 | POST /records/disposal-requests {DisposalType:"Archive", docs, Justification:"انتهاء مدة الاحتفاظ المقررة قانونياً"} | Status=Pending | ☐ |
| 3 | Add legal-hold doc to request | 400 Blocked | ☐ |
| 4 | Records Manager approves | Status=Approved | ☐ |
| 5 | IT Admin executes disposal | Status=Executed | ☐ |
| 6 | GET /documents/{docId} | Status=ARCHIVED | ☐ |
| 7 | GET /audit/logs?eventType=DisposalExecuted | Full disposal log | ☐ |

**Pass Criteria:** Legal hold blocks disposal, approval required, full audit log generated

---

## SCENARIO 7: Business Workspace Lifecycle

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | POST /workspaces {TitleAr:"مشروع التحول الرقمي", TypeId:1} | Status=DRAFT, WorkspaceNumber generated | ☐ |
| 2 | POST /workspaces/{wsId}/activate | Status=ACTIVE | ☐ |
| 3 | POST /workspaces/{wsId}/documents {docId, BindingType:"Primary"} | DocumentCount=1, doc inherits classification | ☐ |
| 4 | POST /workspaces/{wsId}/close "اكتمل المشروع" | Status=CLOSED | ☐ |
| 5 | POST /workspaces/{wsId}/documents (new binding) | 400 "مساحة عمل مغلقة" | ☐ |
| 6 | POST /workspaces/{wsId}/archive | Status=ARCHIVED, docs archived | ☐ |
| 7 | GET /documents/{docId} | Status=ARCHIVED (cascade) | ☐ |
| 8 | GET /workspaces/{wsId}/audit | All lifecycle events in order | ☐ |

**Pass Criteria:** Status progression enforced, cascade works, audit log complete

---

## SCENARIO 8: External System Binding (SAP Sync)

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | POST /workspaces/{wsId}/external-binding {SAP_PROD, WBS-2026-001, WBSElement} | IsBoundToExternal=true, SyncStatus=Pending | ☐ |
| 2 | Same binding on different workspace | 400 "مرتبطة بالفعل" | ☐ |
| 3 | POST /workspaces/{wsId}/sync?direction=Inbound | IsSuccess=true, FieldsUpdated>0 | ☐ |
| 4 | GET /workspaces/{wsId}/metadata | Fields populated from SAP | ☐ |
| 5 | Manual conflict → ExternalWins sync | ECM field overwritten | ☐ |
| 6 | Manual conflict → Manual strategy | SyncStatus=Conflict | ☐ |
| 7 | POST /sync/conflicts/{fieldId}/resolve "UseExternal" | Conflict resolved | ☐ |
| 8 | GET /workspaces/{wsId}/sync/history | All sync events recorded | ☐ |

**Pass Criteria:** One-to-one binding enforced, all conflict strategies work, history complete

---

## SCENARIO 9: Permission Changes Mid-Process

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | User A downloads document (has permission) | 200 OK | ☐ |
| 2 | Admin revokes download permission from User A | Role updated | ☐ |
| 3 | User A's JWT expires + re-login | New token without permission | ☐ |
| 4 | User A attempts download | 403 Forbidden | ☐ |
| 5 | Admin grants documents.access.secret | Updated | ☐ |
| 6 | User A accesses SECRET document | 200 OK | ☐ |
| 7 | GET /audit/logs | Both denied + granted events logged | ☐ |

**Pass Criteria:** Permissions effective after JWT expiry (max 15min), both tested, audited

---

## SCENARIO 10: Concurrency Race Condition Protection

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | Two users simultaneously POST /checkout same doc | One 200, one 400 "محجوزة" | ☐ |
| 2 | Two users simultaneously approve same workflow task | One 200, one 400 "مكتملة" | ☐ |
| 3 | Two admins apply legal hold simultaneously | One 200, one 400 "مطبق بالفعل" | ☐ |
| 4 | DB RowVersion conflict | ECM_006 error "يرجى تحديث الصفحة" | ☐ |

**Pass Criteria:** No data corruption, exactly one winner per operation, ECM_006 error code used

---

## SCENARIO 11: Complete Audit Trail

| Step | Action | Expected Result | Status |
|------|--------|----------------|--------|
| 1 | GET /audit/logs?dateFrom=today | All events listed | ☐ |
| 2 | GET /audit/summary | Counts by type, top users | ☐ |
| 3 | GET /audit/export?format=Excel | Valid Excel file | ☐ |
| 4 | Verify CorrelationId links related events | Same ID across request chain | ☐ |
| 5 | Every 4xx has audit entry | No missed failures | ☐ |
| 6 | GET /workspaces/{wsId}/audit | Workspace-specific events | ☐ |

**Pass Criteria:** 100% critical actions audited, export works, CorrelationId present, immutable records

---

## UAT Sign-off Sheet

| Scenario | Tester | Result | Defects | Date |
|----------|--------|--------|---------|------|
| 1 — Document Lifecycle | | ☐ Pass ☐ Fail | | |
| 2 — Version Control | | ☐ Pass ☐ Fail | | |
| 3 — Classification Control | | ☐ Pass ☐ Fail | | |
| 4 — Legal Hold Cascade | | ☐ Pass ☐ Fail | | |
| 5 — SLA Breach | | ☐ Pass ☐ Fail | | |
| 6 — Disposal Workflow | | ☐ Pass ☐ Fail | | |
| 7 — Workspace Lifecycle | | ☐ Pass ☐ Fail | | |
| 8 — External Integration | | ☐ Pass ☐ Fail | | |
| 9 — Permission Changes | | ☐ Pass ☐ Fail | | |
| 10 — Concurrency | | ☐ Pass ☐ Fail | | |
| 11 — Audit Trail | | ☐ Pass ☐ Fail | | |

**UAT Approval — Go-Live Authorization:**
- [ ] All 11 scenarios PASSED — Approved for Production
- Business Owner: _________________________ Date: ___________
- IT Director: ____________________________ Date: ___________
- Records Manager: _______________________ Date: ___________
