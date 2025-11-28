Param(
    [string]$RepoPath = '.'
)

Write-Host "Interactive helper: remove a literal secret from git history (prints safe commands, does not run destructive commands)."

Push-Location $RepoPath
try {
    Write-Host "Creating a backup bundle of the repository (safe)."
    git bundle create repo-backup.bundle --all

    $secret = Read-Host -AsSecureString "Enter the exact secret string to remove from history (will not be echoed)"
    $plainSecret = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret))
    if ([string]::IsNullOrEmpty($plainSecret)) {
        Write-Host "No secret provided. Exiting." -ForegroundColor Yellow
        return
    }

    # Create a local replacements file outside of tracked files
    $replPath = Join-Path $env:TEMP "replacements.txt"
    $replacementEntry = "$plainSecret==>REDACTED"
    Set-Content -Path $replPath -Value $replacementEntry -NoNewline

    Write-Host "Created temporary replacements file at: $replPath"
    Write-Host "The file contains your secret locally and is NOT added to the repo."

    Write-Host "\nRecommended (git-filter-repo) - run the following commands manually in this repository:" -ForegroundColor Cyan
    Write-Host "git status --porcelain --untracked-files=no" -ForegroundColor Green
    Write-Host "# Verify clean working tree before rewriting history" -ForegroundColor Gray
    Write-Host "git filter-repo --replace-text $replPath --refs refs/heads/main" -ForegroundColor Green
    Write-Host "# After that, force-push the rewritten history:" -ForegroundColor Gray
    Write-Host "git push origin --force --all" -ForegroundColor Green
    Write-Host "git push origin --force --tags" -ForegroundColor Green

    Write-Host "\nAlternative (BFG) - if you prefer BFG, you can mirror-clone and use bfg.jar:" -ForegroundColor Cyan
    Write-Host "git clone --mirror . repo.git" -ForegroundColor Green
    Write-Host "java -jar bfg.jar --replace-text $replPath repo.git" -ForegroundColor Green
    Write-Host "cd repo.git" -ForegroundColor Green
    Write-Host "git reflog expire --expire=now --all && git gc --prune=now --aggressive" -ForegroundColor Green
    Write-Host "git push --force" -ForegroundColor Green

    Write-Host "\nAfter rewriting history and pushing, inspect GitHub Security -> Secret scanning and use the unblock URL from the push failure if needed." -ForegroundColor Cyan

} finally {
    Pop-Location
}
