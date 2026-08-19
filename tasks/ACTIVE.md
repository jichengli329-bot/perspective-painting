# Active delivery cycle

Status: IN PROGRESS

## T-038 — Public repository and playtest release preparation

Prepare the current four-gallery Perspective Painting vertical slice for a safe public or private GitHub publication, pending the product owner's explicit choice.

Scope:

- produce an accurate public README and current playtest guide;
- document repository/Release/itch.io publishing boundaries;
- audit tracked size, ignored Unity caches, credentials and remote state;
- confirm repository visibility and license before creating the remote;
- install/authenticate the required GitHub tooling without storing credentials in the repository;
- push the verified source history and upload a complete zipped Windows build as a GitHub Release;
- verify remote access before removing any regenerable local cache.

Acceptance:

- owner confirms GitHub visibility and license;
- remote repository contains the intended tracked source and no credentials/caches;
- `v0.1.0-playtest` Release contains a downloadable Windows x64 ZIP that runs after clean extraction;
- local repository remains clean and all remote commits are present;
- only verified regenerable local folders are removed, with reclaimed space reported.

Current evidence:

- local Git worktree was clean at T-038 start and has no remote;
- 719 tracked files total approximately 26.84 MiB; largest tracked file is 2.18 MiB;
- no credential-like tracked file match was found by the filename-only safety scan;
- ignored local data includes Library ~1.72 GiB, Logs ~0.13 GiB and Builds ~0.31 GiB;
- GitHub CLI is not currently installed;
- owner confirmed a Public repository with all rights reserved;
- README, proprietary LICENSE, publishing specification and current four-gallery playtest guide are prepared;
- the 62.3 MiB Windows candidate ZIP was clean-extracted and remained alive for a 15-second smoke with zero targeted runtime error matches.
