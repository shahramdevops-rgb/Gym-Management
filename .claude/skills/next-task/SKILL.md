---
name: next-task
description: Start a roadmap task from docs/ROADMAP.md using the plan, build, test, teach loop. Invoke with /next-task <task id>, for example /next-task 0.1.
disable-model-invocation: true
---

Start roadmap task: $ARGUMENTS
If no task id was given, use the first task in docs/ROADMAP.md that still has unchecked items.

1. Read the task in docs/ROADMAP.md. Read only the related sections of docs/BUSINESS_RULES.md and docs/ARCHITECTURE.md.
2. Run `git status`. If there are uncommitted changes, stop and tell me.
3. Create a branch named `task/<id>-<short-name>` from main.
4. Before planning, explain in 3 to 6 plain sentences the concepts this task teaches and why they matter in this project.
5. Present a plan:
   - files to create or change
   - packages to add (only from the approved list)
   - tests you will write, including failure cases
   - any business rule that is unclear (ask, do not guess)
   Then STOP and wait for my approval.
6. After approval, implement in small steps. Run `dotnet build` after each step.
7. Write the planned tests. Run `dotnet build` and `dotnet test` (and `npm run lint`, `npm test`, `npm run build` if web/ changed). Iterate until everything is green. Show the final command output.
8. Verify the task's "Done when" check and show the evidence.
9. Teaching mode: summarize what you built and why, list the new concepts, and append them to docs/LEARNING.md under the task id.
10. Tick the completed items in docs/ROADMAP.md.
11. Do not commit. Tell me the 3 most important files to read first, and suggest a Conventional Commit message.
