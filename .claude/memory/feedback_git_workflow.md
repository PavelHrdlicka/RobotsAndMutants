---
name: Git commit after each tested change
description: After every code change that passes tests, commit and push to GitHub
type: feedback
---

After every successful code modification (especially when tests pass), commit all changes and push to GitHub.

**Why:** User explicitly requested this — they want each tested change tracked in version control at https://github.com/PavelHrdlicka/RobotsAndMutants

**How to apply:**
- Working directory: d:\_HERD\ROBOTI-A-MUTANTI\RobotsAndMutants
- Remote: origin (https://github.com/PavelHrdlicka/RobotsAndMutants)
- Branch: main
- After completing a feature/fix with tests → `git add` relevant files → `git commit` with descriptive message → `git push`
- Use conventional commit style, include Co-Authored-By trailer
