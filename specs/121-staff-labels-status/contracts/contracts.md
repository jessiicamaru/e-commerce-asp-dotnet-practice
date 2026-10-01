# Contracts: Every audit action has words, and the status page lists every service

No HTTP, message or gRPC shape changes. The status page calls the two health routes it skipped (`/api/orchestrator/health`, `/api/activity/health`), which exist and are anonymous.
