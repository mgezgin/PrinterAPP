# How to Create a Release for Printer App

## Step 1: Update Version Number

Edit `PrinterAPP/PrinterAPP/PrinterAPP.csproj` and increment the version:

```xml
<Version>1.0.1</Version>
<ApplicationDisplayVersion>1.0.1</ApplicationDisplayVersion>
```

## Step 2: Build Release Binary

Run from `PrinterAPP/PrinterAPP` directory:

```bash
dotent publish -c Release -r win-x64 --self-contained
```

The compiled `.exe` will be in:
```
bin/Release/net9.0-windows10.0.19041.0/win-x64/publish/PrinterAPP.exe
```

## Step 3: Create GitHub Release

1. Go to: https://github.com/mahmutkaya/rumi-restaurant/releases/new

2. Create a new tag:
   - Tag: `v1.0.1` (must match version in csproj)
   - Target: `main` branch

3. Fill release details:
   - **Title**: `Printer App v1.0.1`
   - **Description**: List what's new/fixed in this version

4. Upload the compiled exe:
   - Click "Attach binaries"
   - Upload `PrinterAPP.exe` from the publish folder

5. Click "Publish release"

## Step 4: Restaurant Can Now Update

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

## Testing Before Release

1. Build release locally
2. Test the exe on a local machine
3. Verify all features work
4. Create release only when confident

## Troubleshooting

**Q: Update check fails with "API rate limit exceeded"**
A: GitHub API has rate limits. Wait a few minutes and try again.

**Q: Download fails or file is corrupted**
A: Ensure the exe was uploaded correctly to GitHub release assets.

**Q: App doesn't restart after update**
A: Check Windows permissions. App needs write access to its directory.
