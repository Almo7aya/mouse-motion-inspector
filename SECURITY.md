# Security and privacy

Mouse Motion Inspector operates locally. It does not upload recordings, install
drivers, modify firmware, change mouse settings or record keystrokes. It listens
for mouse input in the current Windows session, including when another app is
focused. Closing the app unregisters the listener, hook and hotkey.

JSON/CSV exports may contain HID interface paths, device identifiers, mouse-button
flags, screen coordinates and user-entered notes. PNG charts contain device
labels and movement geometry. Review exports before sharing. Recordings are not
encrypted. Local captures and generated artifacts are excluded by `.gitignore`.

Report security issues through [GitHub private vulnerability reporting](https://github.com/Almo7aya/mouse-motion-inspector/security/advisories/new)
(**Security → Report a vulnerability**). Do not post exploit details, secrets or
private captures in a public issue. Ordinary bug reports can use the issue tracker.

Recording import accepts bounded version-1 JSON, validates identities, times and
movement totals, and does not execute content or follow paths embedded in the
file. Unsupported formats are rejected. Keep .NET and Windows up to date.

Release archives are unsigned unless a maintainer explicitly adds signing.
SHA-256 files detect corruption only when obtained from a trusted source; they
are not proof of publisher identity.
