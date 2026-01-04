# How to Create a Release for Printer App (Automated)

## Step 1: Update Version Number

Edit `PrinterAPP/PrinterAPP/PrinterAPP.csproj` and increment the version:

```xml
<Version>1.0.3</Version>
<ApplicationDisplayVersion>1.0.3</ApplicationDisplayVersion>
```

## Step 2: Commit and Tag

GitHub Actions will automatically build the Windows `.exe` and upload it when you push a new tag.

1. Commit your changes:
   ```bash
   git add .
   git commit -m "chore: bump version to 1.0.3"
   git push
   ```

2. Create and push a tag:
   ```bash
   git tag v1.0.3
   git push origin v1.0.3
   ```

## Step 3: Wait for Build

1. Go to your GitHub repository -> **Actions** tab.
2. You will see a "Build and Release" workflow running.
3. Wait ~5 minutes for it to complete.

## Step 4: Publish Release

1. Go to **Releases**.
2. You will see a new release created automatically (or a draft).
3. The file `PrinterApp-Setup.exe` will be attached automatically.
4. Edit the release to add release notes if desired.

## Step 5: Restaurant Can Now Update

Restaurant users:
1. Open Printer App
2. Click "🔄 Update" button
3. Click "Check for Updates"
4. If update available, click "Update Now"
5. App automatically downloads, installs, and restarts

## Version Numbering

- **Major.Minor.Patch** format (e.g., 1.0.1)
- **Patch** (0.0.X): Bug fixes, small improvements
- **Minor** (0.X.0): New features, non-breaking changes
- **Major** (X.0.0): Breaking changes, major rewrites

## Troubleshooting

**Q: Build failed on GitHub Actions**
A: Check the Actions logs. Ensure code compiles locally.

**Q: Tag pushed but no release created**
A: Check `.github/workflows/build-release.yml` syntax or permissions.

**Q: App update fails**
A: Ensure the generated `PrinterApp-Setup.exe` is roughly 60MB+ in size (it includes .NET runtime).
