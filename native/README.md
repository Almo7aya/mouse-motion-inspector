# Windows application

Start with the [project README](../README.md) for building and running Mouse Motion Inspector.

- [Interpreting recordings](../docs/interpretation.md)
- [Architecture and diagnostic commands](../docs/architecture.md)
- [Release validation](../docs/release-checklist.md)

Projects: `MouseMotionInspector.Core` handles recording arithmetic, import/export
and plot data. `MouseMotionInspector.Native` contains Windows capture, the themed
interface and charts. `MouseMotionInspector.Checks` contains deterministic checks
and optional saved-recording validation.
