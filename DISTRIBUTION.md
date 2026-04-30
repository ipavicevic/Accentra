# Accentra — Distribution Plan

## Target stack

| Layer | Tool | Purpose |
|-------|------|---------|
| Source & CI | Azure DevOps | Code, backlog, build pipeline |
| Release artifacts | GitHub Releases | Public download, versioned EXEs |
| Landing page | GitHub Pages | Description, user manual, download button |
| Package manager | WinGet | `winget install Accentra` (future) |

---

## Setup steps

### 1. GitHub repo
- Create public repo `github.com/ipavicevic/Accentra`
- Initialize with a README (no need to push source code)

### 2. GitHub Personal Access Token
- GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
- Scopes: `repo` (full)
- Store as a secret variable `GITHUB_PAT` in ADO (Pipelines → Library)

### 3. ADO pipeline — publish to GitHub Releases
Add a step to `azure-pipelines.yml` after the publish step:
- Create a GitHub Release for the tag using the GitHub API
- Upload `Accentra.exe` and `accent-maps.json` as release assets
- Mark as latest release

### 4. GitHub Pages landing page
- Add a `docs/` folder to the GitHub repo with `index.html`
- Enable GitHub Pages in repo Settings → Pages → Source: `main / docs`
- Page contents:
  - App name + tagline
  - Short description of how it works
  - Animated demo (GIF or video)
  - Download button (links to latest GitHub Release)
  - User manual (how to install, how to use, how to uninstall)
  - Accent maps reference table
  - System requirements (Windows 10/11)

### 5. WinGet submission *(after first stable public release)*
- Fork `microsoft/winget-pkgs` on GitHub
- Add a manifest under `manifests/i/ipavicevic/Accentra/1.0.0/`
- Open a PR — automated validation runs, maintainers merge
- Users can then: `winget install ipavicevic.Accentra`

---

## Release workflow (ongoing)

```
git tag v1.x.x
git push origin v1.x.x
        ↓
ADO pipeline triggers
        ↓
Builds self-contained EXE
        ↓
Creates GitHub Release v1.x.x
Uploads Accentra.exe + accent-maps.json
        ↓
Landing page download button always points to /releases/latest
```

---

## Pending

- [ ] Create GitHub repo `ipavicevic/Accentra`
- [ ] Generate GitHub PAT and store in ADO as `GITHUB_PAT`
- [ ] Update `azure-pipelines.yml` with GitHub Release step
- [ ] Create `docs/index.html` landing page
- [ ] Enable GitHub Pages
- [ ] Re-run pipeline on `v1.0.0` to publish first release
