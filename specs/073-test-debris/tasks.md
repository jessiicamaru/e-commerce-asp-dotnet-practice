# Tasks: Test runs clean up after themselves

- [ ] T001 `bruno/cleanup/`: the folder, run last, and three deletes, each asserted 204 or 404.
- [ ] T002 A `trap cleanup EXIT` in `.github/scripts/verify-saga.sh` and `verify-auth.sh`. Deletes are reported and never fatal, and the exit status is kept.
- [ ] T003 Evidence: the product count before and after two Bruno runs and one `verify-saga.sh` run, plus a forced failure that still cleans up and exits non-zero. Docs: CLAUDE.md, the testing strategy, the timeline and the backlog.
