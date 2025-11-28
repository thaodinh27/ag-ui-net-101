# Cleaning Sensitive Data From Git History

This guide shows a safe, recommended workflow to remove sensitive secrets (API keys, tokens, etc.) from git history and push the cleaned history to a remote repository. Follow the steps carefully — rewriting history is destructive and will require collaborators to re-clone or reset their local clones.

## 1) Prepare and back up

- Ensure you have a clean working tree. Commit or stash any changes before proceeding.

cd D:\\Exercises\\ag-ui-net-101
git status --porcelain
git bundle create repo-backup.bundle --all

Keep `repo-backup.bundle` somewhere safe until you're confident the cleanup is successful.

## 2) Remove secrets from the working tree

- Replace hard-coded secrets in files with environment variable usage, key vault references, or configuration placeholders. For local development, use a `.env` file and add it to `.gitignore`.

Example: In `agentic-agent-101/AgenticAgent.cs` replace:

var key = "your-api-key";

with

var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY") ?? string.Empty;

Add a `.env.example` with placeholders and ensure `.env` is ignored.

## 3) Prepare replacements file (local only)

- Create a local `replacements.txt` file (DO NOT commit this file). Use the template at `scripts/replacements-template.txt`.

Copy-Item .\\scripts\\replacements-template.txt .\\replacements.txt
notepad .\\replacements.txt
# Replace <PASTE_SECRET_1_HERE> with the exact secret string

Or use the interactive script which creates a temporary replacements file for you:

.\\scripts\\remove-secret.ps1
# The script prints the temp replacements file path (e.g. C:\\Users\\You\\AppData\\Local\\Temp\\replacements.txt)

## 4) Dry run and analysis (non-destructive)

# Install git-filter-repo if needed
python -m pip install --user git-filter-repo
# Non-destructive analysis
python -m git_filter_repo --analyze
# Dry-run of the intended replacement
python -m git_filter_repo --replace-text .\\replacements.txt --refs refs/heads/main --dry-run

## 5) Perform the history rewrite (recommended: fresh clone)

Recommended approach: operate in a fresh clone to reduce risk.

cd ..
git clone D:\\Exercises\\ag-ui-net-101 ag-ui-net-101-fresh
cd ag-ui-net-101-fresh
# Copy your local replacements.txt into this fresh clone root (do not commit it)
python -m git_filter_repo --replace-text .\\replacements.txt --refs refs/heads/main

If you must run in-place, ensure the working tree is clean then run with --force:

cd D:\\Exercises\\ag-ui-net-101
git status --porcelain
python -m git_filter_repo --replace-text .\\replacements.txt --refs refs/heads/main --force

## 6) Verify locally

# Search for occurrences in working tree and in history
git grep -n '<your-secret-substring>' || echo 'No occurrences in working tree'
git log -S '<your-secret-substring>' --all || echo 'No occurrences in commit history'

## 7) Push rewritten history to remote

- Inform collaborators that you will force-push and they will need to reclone or reset.

git push origin --force --all
git push origin --force --tags

## 8) Post-push: GitHub secret scanning and cleanup

- Check the repository Security → Secret scanning page. If the secret was detected before, it may still show until scanning updates. Use the unblock URL provided in the push failure message or the Secret scanning UI to clear flagged secrets.

## 9) Notify collaborators

- After pushing rewritten history, ask collaborators to re-clone or reset their local clones. A common recovery command for collaborators is:

# Warning: this discards local commits that diverge from the rewritten history
git fetch origin
git reset --hard origin/main

## 10) Prevent future leaks

- Use environment variables or a secrets manager (Azure Key Vault, AWS Secrets Manager, GCP Secret Manager).
- Add `.env` to `.gitignore` (already added).
- Consider pre-commit hooks like `git-secrets` or `pre-commit` to block accidental commits of secrets.

## Appendix: Alternative using BFG Repo-Cleaner

If you prefer BFG, use a mirror clone and run BFG:

git clone --mirror D:\\Exercises\\ag-ui-net-101 repo.git
# Create replacements.txt locally with secret(s)
java -jar bfg.jar --replace-text replacements.txt repo.git
cd repo.git
git reflog expire --expire=now --all && git gc --prune=now --aggressive
git push --force

## Notes and warnings

- Rewriting history is irreversible for practical purposes. Keep backups and coordinate with your team.
- Do not add secrets to tracked files again. Use `.env` for local dev or a managed secret store for production.
