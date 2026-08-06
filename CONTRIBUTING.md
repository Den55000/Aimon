# Contributing to Aimon

A lightweight workflow that keeps history clean and `main` always releasable.

## Branching model

- **`main`** is protected and always builds + passes tests. Never commit directly to it.
- Do work on short-lived **feature branches** cut from an up-to-date `main`.

Branch naming — `type/short-description`:

| Type       | Use for                                  | Example                          |
|------------|------------------------------------------|----------------------------------|
| `feat/`    | new functionality                        | `feat/openai-provider`           |
| `fix/`     | bug fixes                                | `fix/streaming-cancellation`     |
| `chore/`   | tooling, deps, build, no product change  | `chore/bump-avalonia`            |
| `docs/`    | documentation only                       | `docs/architecture-adr`          |
| `refactor/`| behavior-preserving restructuring        | `refactor/router-context-builder`|
| `test/`    | tests only                               | `test/state-reducer-cases`       |

## Commit messages — [Conventional Commits](https://www.conventionalcommits.org/)

```
type(scope): short imperative summary

Optional body explaining what and why (not how).
```

Examples:

```
feat(interpreter): build OpenAI request from execution plan
fix(router): enforce hard settings before dispatch
chore(build): add warnings-as-errors to Directory.Build.props
```

Guidelines:
- One logical change per commit; keep them small and reviewable.
- Present-tense, imperative mood ("add", not "added").
- Reference issues where relevant: `Closes #12`.

## Day-to-day loop

```bash
git switch main
git pull --ff-only                      # get latest

git switch -c feat/my-thing             # new branch
# … make changes …
dotnet build Aimon.slnx -c Release      # build must be green
dotnet test  Aimon.slnx -c Release      # tests must pass

git add -A
git commit -m "feat(scope): summary"
git push -u origin feat/my-thing        # first push sets upstream
```

Then open a Pull Request into `main`, get it reviewed, and **squash-merge**. Delete the branch
after merge.

## Keeping a branch current

```bash
git switch feat/my-thing
git fetch origin
git rebase origin/main                  # replay your work on top of latest main
# resolve conflicts, then: git rebase --continue
git push --force-with-lease             # safe force-push after a rebase
```

## Never commit

- Build output (`bin/`, `obj/`) — already covered by `.gitignore`.
- Secrets: API keys, tokens, `*.env`, `secrets.json`. Keys belong in the OS secret store, never
  in a profile or the repo.
- Large binaries or generated artifacts.

## Before pushing — checklist

- [ ] `dotnet build -c Release` → 0 warnings, 0 errors (warnings are errors here).
- [ ] `dotnet test` → green.
- [ ] No secrets or build output staged (`git status` to confirm).
- [ ] Commit messages follow the convention above.
