# Active delivery cycle

Status: COMPLETE

## T-038 — Public repository and playtest release

The four-gallery Perspective Painting vertical slice is publicly available as a proprietary source-visible repository and a verified Windows playtest Release.

Delivery:

- public repository: `https://github.com/jichengli329-bot/perspective-painting`;
- Release: `https://github.com/jichengli329-bot/perspective-painting/releases/tag/v0.1.0-playtest`;
- Windows asset: `PerspectivePainting-v0.1.0-playtest-windows-x64.zip`;
- remote state: `uploaded`, 65,276,277 bytes;
- SHA-256: `B8B70F8F0A4CE3845D78631D0A65C196429E082DDE0AAB06960807DEE33713CC`;
- visibility: Public;
- license: proprietary, all rights reserved.

Verification:

- tracked-file and credential-name audits passed before publication;
- the candidate was clean-extracted and survived the 15-second standalone smoke window with zero targeted runtime-error matches;
- GitHub reports the same SHA-256 digest as the locally verified ZIP;
- the Release was reduced to one complete ZIP asset after remote verification;
- the temporary writable SSH deploy key was deleted and verified absent;
- `Library`, `Logs`, `Builds`, `TestResults` and `outputs` were removed only after remote verification;
- local cleanup reclaimed 2,799,135,367 bytes (about 2.61 GiB).

Publication note:

- the public repository starts with a compact release snapshot rather than the entire private development history because the available GitHub route repeatedly broke long Git transfers;
- seven large internal art-direction process images were intentionally omitted from the public snapshot; they are not runtime dependencies;
- the complete local Git history remains in `.git`.
