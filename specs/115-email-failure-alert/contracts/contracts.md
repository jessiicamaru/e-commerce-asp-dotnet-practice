# Contracts: Administrators are told when an email fails for good

## Notification kind `EmailsFailed`

`notification-kinds.json`:

```json
"EmailsFailed": { "required": ["failed"] }
```

and the placeholder `"failed": ["failed"]`. Link: `/admin/email-delivery`. Recipients: every `Admin`.

Default words - en: "{{failed}} emails could not be delivered after every attempt. Open the email log to retry them."
vi: "{{failed}} email không gửi được sau mọi lần thử. Mở nhật ký email để gửi lại."

## HTTP

No new endpoint. The Overview reads `GET /api/emails?status=Failed&page=1&pageSize=1` (Admin) and shows `totalCount`.
