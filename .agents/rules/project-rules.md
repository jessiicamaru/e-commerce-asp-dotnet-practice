---
name: project-rules
version: 1.0.0
priority: P0
trigger: always_on
description: Project specific rules for Habit Tracker (mandatory constraints and conventions).
---

# Project Specific Rules (Habit Tracker)

1. **Test Cases**: All new features and core logic MUST be accompanied by appropriate test cases (unit tests, widget tests, or integration tests).
2. **Follow Conventions**: Strictly adhere to the technology-specific conventions (e.g., Clean Architecture, Feature-first in Flutter, CQRS in .NET).
3. **Clean Code**: Code must be readable, maintainable, and well-structured. Follow SOLID principles.
4. **No Magic Numbers/Strings**: Do NOT use hardcoded strings or numbers. Extract them into configuration files, constant classes, or enums.
5. **Backend Architecture**: Whenever using Clean Architecture in the .NET backend, it MUST be paired with MediatR and the CQRS pattern.
6. **Database Name**: The database name must be `habit-tracker`.
7. **Shadcn UI & Icons**: MUST use official `shadcn_ui` components and official `lucide_icons` to ensure UI consistency and avoid a messy interface.
8. **Folder Structure**: Separate folders clearly by feature/layer. Do NOT mix components or layers into single generic folders.
9. **Small Components**: Do NOT leave components too large. Always separate UI components into smaller, reusable widgets.
10. **No Auto Git Operations**: Do NOT automatically checkout new branches, commit code, or push to remote repositories unless explicitly requested by the user.
11. **Unit Tests are Mandatory**:
    - Every new feature, command handler, repository, or utility function MUST have corresponding unit tests written at the same time.
    - Every time existing code is modified, the related unit tests MUST be updated to reflect the change.
    - Before delivering any result or ending a task, you MUST run all unit tests (e.g. `dotnet test` for .NET, `flutter test` for Flutter) and report the results.
    - Do NOT deliver a response without showing test results. If tests fail, fix them first.

12. **Git Workflow Restrictions**: Do NOT make small, fragmented commits (commit lắt nhắt). You MUST ask for explicit permission from the user BEFORE committing code, pushing code, or checking out branches.

13. **Android Native Plugins**: When adding Flutter plugins that use native Android code (e.g. `flutter_secure_storage`, `wakelock_plus`), always verify `minSdk` compatibility. `flutter_secure_storage` requires `minSdk = 21`. After adding native plugins, a full cold restart of `flutter run` is required — Hot Reload alone is NOT sufficient to register native plugins.

14. **Mandatory Testing Before Completion**: Always run unit tests (for both Backend and Frontend) and E2E tests (for Frontend) before marking a task as complete ("done"). NEVER mark a task as done without first running and passing these tests.

15. **Mandatory Cross-Validation & Refactoring Checks (No Trial-and-Error)**: 
    - **Widget Internal Logic**: Before passing children (like `Expanded` or `Flex`) to a third-party UI component (e.g., `Shadcn UI`), check its internal source/documentation to ensure it doesn't break layout constraints (like nesting inside `MouseRegion`).
    - **API Changes & Tests**: Whenever modifying a widget's parameters (e.g., changing from `required` to `optional`), you MUST immediately update all dependent tests in the same step. Do not wait for tests to fail to fix compilation errors.
    - **Network Dependency Mocking**: Whenever adding a widget that fetches network data (via providers/Dio) into a parent widget (like a Dialog or Sheet), you MUST immediately add the appropriate Provider override (`apiServiceProvider.overrideWithValue`) in the parent's tests to prevent real HTTP requests or pending timers from failing the test suite. 
    - **No blind fixes**: Do not try a 1-line fix, run tests, see a failure, and try another 1-line fix. Read the full error, trace the entire flow, and fix all related occurrences at once.

16. **Mandatory Static Analysis**: After modifying code (especially structural UI changes or multiple files) and before marking a task as done, you MUST run static analysis tools (e.g., `flutter analyze` or `dart analyze` for Flutter, `dotnet build` for Backend) in the background to catch syntax errors, missing brackets, or type mismatches early. Do not wait for Hot Reload to crash.
