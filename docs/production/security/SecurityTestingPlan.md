# DARAH ECM — Security Testing Plan
# خطة اختبار الأمان | Version: 1.0 | April 2026

---

## 1. AUTHENTICATION & AUTHORIZATION

### 1.1 JWT Token Security
| Test | Method | Expected Result | Status |
|------|--------|----------------|--------|
| JWT signature validation | Send tampered token | 401 Unauthorized | ☐ |
| JWT expiry enforcement | Use expired token (after 15min) | 401 Unauthorized | ☐ |
| JWT algorithm confusion | Send token with alg=none | 401 Rejected | ☐ |
| Refresh token rotation | Use refresh token twice | Second use returns 401 | ☐ |
| Token from different user | Use valid token but wrong userId claim | 403 Forbidden | ☐ |

### 1.2 Access Control
| Test | Method | Expected Result | Status |
|------|--------|----------------|--------|
| IDOR on document | GET /documents/{otherUserId_doc} | 403 or 404 | ☐ |
| IDOR on workspace | GET /workspaces/{otherUser_ws} | 403 or 404 | ☐ |
| Privilege escalation | Basic user calls admin endpoint | 403 Forbidden | ☐ |
| SECRET doc without permission | Access classification=4 doc | 403 + no info leaked | ☐ |
| Missing Authorization header | Call any endpoint without Bearer | 401 Unauthorized | ☐ |
| Horizontal privilege escalation | User from Dept A access Dept B docs | 403 Forbidden | ☐ |

---

## 2. INPUT VALIDATION & INJECTION

### 2.1 SQL Injection
| Test | Payload | Expected Result | Status |
|------|---------|----------------|--------|
| Login endpoint | username="admin' OR '1'='1" | 400 ValidationError, no SQL error exposed | ☐ |
| Document search | textQuery="'; DROP TABLE Documents;--" | Safe response, no DB change | ☐ |
| ID parameters | /documents/1 OR 1=1 | 400 or 404 | ☐ |

### 2.2 File Upload Security
| Test | File | Expected Result | Status |
|------|------|----------------|--------|
| Executable upload | malware.exe renamed to report.pdf | 400 "توقيع الملف لا يطابق الامتداد" | ☐ |
| MIME type spoofing | EXE with Content-Type: application/pdf | 400 Rejected by magic bytes check | ☐ |
| Zero-byte file | Empty file | 400 "الملف فارغ" | ☐ |
| Oversized file | 600MB file (limit=512MB) | 400 "حجم الملف يتجاوز الحد" | ☐ |
| Path traversal in filename | ../../etc/passwd.pdf | Sanitized, safe storage key | ☐ |
| Script in filename | <script>alert(1)</script>.pdf | Sanitized | ☐ |
| ZIP bomb | Deeply nested ZIP (expansion attack) | Rejected or size-limited | ☐ |

### 2.3 Cross-Site Scripting (XSS)
| Test | Payload | Expected Result | Status |
|------|---------|----------------|--------|
| Document title | TitleAr="<script>alert(1)</script>" | Stored as-is, escaped in JSON response | ☐ |
| Metadata field | "<img src=x onerror=alert(1)>" | Escaped in response | ☐ |
| API response Content-Type | All endpoints | application/json (not text/html) | ☐ |

### 2.4 API Security
| Test | Method | Expected Result | Status |
|------|--------|----------------|--------|
| Mass assignment | POST with extra unexpected fields | Extra fields ignored | ☐ |
| HTTP verb tampering | TRACE/DEBUG method | 405 Method Not Allowed | ☐ |
| Rate limiting bypass | 100 req/min to auth endpoint | 429 after 10 req/min | ☐ |
| Large payload | 10MB JSON body | 413 Payload Too Large | ☐ |

---

## 3. DATA PROTECTION

### 3.1 Data in Transit
| Test | Method | Expected Result | Status |
|------|--------|----------------|--------|
| HTTP redirect | Access http:// URL | 301 redirect to https:// | ☐ |
| TLS version | Check supported protocols | TLS 1.2 + 1.3 only, no SSL 3/TLS 1.0/1.1 | ☐ |
| Cipher strength | SSL Labs test | Grade A minimum | ☐ |
| HSTS header | GET response headers | max-age=63072000 present | ☐ |

### 3.2 Sensitive Data Exposure
| Test | Scenario | Expected Result | Status |
|------|----------|----------------|--------|
| Error messages | Trigger 500 error | Generic Arabic message, no stack trace | ☐ |
| 403 response body | Access denied document | No document title/content in response | ☐ |
| SQL errors | Force DB error | Generic ECM_099 error, no SQL details | ☐ |
| JWT payload | Decode JWT | No passwords/secrets in claims | ☐ |
| Audit log | GET /audit/logs | No plaintext passwords in log entries | ☐ |
| File storage keys | API response | Storage keys not predictable/sequential | ☐ |

### 3.3 Passwords & Secrets
| Test | Check | Expected Result | Status |
|------|-------|----------------|--------|
| Password hashing | Inspect DB | BCrypt hash (not plaintext) | ☐ |
| Config files in source | grep for secrets in repo | Zero secrets committed | ☐ |
| Environment variables | docker inspect container | Secrets via env vars only | ☐ |
| JWT secret length | Check configuration | Minimum 256-bit (32 chars) | ☐ |

---

## 4. OWASP TOP 10 CHECKLIST

| # | Risk | Test | Status |
|---|------|------|--------|
| A01 | Broken Access Control | IDOR + privilege escalation tests | ☐ |
| A02 | Cryptographic Failures | TLS/cipher tests + password hashing | ☐ |
| A03 | Injection | SQL + XSS + file upload tests | ☐ |
| A04 | Insecure Design | Architecture review — classification + legal hold + ABAC | ☐ |
| A05 | Security Misconfiguration | Server tokens, error messages, CORS | ☐ |
| A06 | Vulnerable Components | `dotnet list package --vulnerable` — zero Critical/High | ☐ |
| A07 | Auth & Session Failures | JWT tests + rate limiting on auth | ☐ |
| A08 | Software Integrity | Image signing, dependency hashing | ☐ |
| A09 | Logging & Monitoring | Verify all security events audited | ☐ |
| A10 | SSRF | External sync connector URL validation | ☐ |

---

## 5. PENETRATION TEST SCOPE

### In-Scope
- `/api/v1/*` all endpoints
- Authentication flow (login, refresh, logout)
- File upload endpoints
- Webhook receiver
- Hangfire dashboard (if exposed)

### Out-of-Scope
- External systems (SAP, Salesforce — test against mock)
- Network infrastructure below application layer
- Denial of Service (coordinate separately)

### Tools Approved
- OWASP ZAP (automated scan)
- Burp Suite Professional
- `dotnet list package --vulnerable`
- SSL Labs (https://www.ssllabs.com/ssltest/)
- Semgrep (SAST)

---

## 6. SECURITY SIGN-OFF

| Test Category | Tester | Critical Issues | Cleared | Date |
|---------------|--------|----------------|---------|------|
| Authentication | | | ☐ | |
| Authorization | | | ☐ | |
| File Upload | | | ☐ | |
| Injection | | | ☐ | |
| Data Protection | | | ☐ | |
| OWASP Top 10 | | | ☐ | |

**Security Clearance for Go-Live:**
- [ ] Zero Critical/High security issues open
- Security Lead: _________________________ Date: ___________
- IT Director:   _________________________ Date: ___________
