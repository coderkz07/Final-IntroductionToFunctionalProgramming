# First push to GitHub

Open PowerShell on the computer with the project. The local repository is on branch `main` and already points to the course repository.

```powershell
Set-Location -LiteralPath 'C:\Users\yerke\OneDrive\Desktop\Study\Final-IntroductionToFunctionalProgramming'
git remote -v
git ls-remote origin
```

Confirm that `origin` is [coderkz07/Final-IntroductionToFunctionalProgramming](https://github.com/coderkz07/Final-IntroductionToFunctionalProgramming). These first-push steps assume `git ls-remote origin` succeeds and lists no refs.

Set the author name and email for this repository. Enter the email linked to your GitHub account or its GitHub noreply address.

```powershell
git config --local user.name "Madiyar Makhan"
git config --local user.email (Read-Host "GitHub email or noreply address")
git var GIT_AUTHOR_IDENT
```

Review the files and make the first commit.

```powershell
git add --all
git diff --cached --stat
git diff --cached --check
git commit -m "Add StudyFlow final project"
```

When you are ready to publish, push `main` and check that the remote branch points to your commit.

```powershell
git push -u origin main
git log -1 --oneline
git ls-remote --heads origin main
```

Use the GitHub account with write access to the repository if Git asks you to sign in. The source code and defense slides are in the same repository. To run the application after the push, follow the [local setup steps](../README.md).
