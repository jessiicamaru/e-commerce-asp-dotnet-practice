<!--
Most pull requests in this repository are opened with the `gh-pr-create` skill, which
fills in a fuller description from its own template. This file exists for the ones that
are not — opened from the web interface, or by anyone not using that skill.

Keep the two in step: .claude/skills/gh-pr-create/templates/pr-description.md
-->

## Summary

<!-- What changes and why. Lead with the user-visible effect, not the implementation. -->

<!-- Closes #N  |  Refs #N  |  delete this line if there is no issue -->

## What changed

<!-- Group by area, not by file. Each bullet says what now behaves differently. -->

## Verification

<!-- What you actually ran, and its real output. "Should work" is not verification. -->

| Check | Result |
| --- | --- |
| `dotnet build` | |
| `dotnet test` | |
| Manual / E2E | |

## Checklist

Only tick what you have verified. An unticked box with a one-line reason is useful; a
ticked box that is not true is worse than no checklist at all.

- [ ] Behaviour that cannot be checked by hand has an automated check
- [ ] `dotnet build` is clean; any test project touched passes
- [ ] Entity writes and the events they cause commit together — publish **before** the
      single `SaveChangesAsync` (constitution III)
- [ ] Any new or changed consumer is safe to process the same message twice, enforced
      by a constraint or a guarded update rather than configuration
- [ ] Caller identity comes from `ICurrentUser`, never from the request (constitution IV)
- [ ] **The previous image can run against this schema, or the change is split into
      expand and contract** — see the constitution, *Technology & Implementation
      Constraints*. A dropped, renamed or narrowed column means redeploying an earlier
      version takes the service down rather than restoring it
- [ ] No secret, token or credential added to a tracked file
- [ ] Docs under `docs/` that this change contradicts were updated in the same change
