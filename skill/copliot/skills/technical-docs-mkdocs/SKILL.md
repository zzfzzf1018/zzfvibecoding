---
name: technical-docs-mkdocs
description: 'Use when creating or maintaining MkDocs or MkDocs Material documentation, README, dev setup, build and test guide, architecture docs, API docs, release docs, docs navigation, mkdocs.yml, Mermaid diagrams, or documentation publishing workflow.'
argument-hint: 'Documentation goal, audience, existing docs path, and whether to create files'
---
# Technical Docs MkDocs

## When to Use
- User wants a documentation site for a project.
- User asks to create or improve README, dev setup, build/test, architecture, or release docs.
- MkDocs, MkDocs Material, `mkdocs.yml`, navigation, or docs publishing is involved.

## Documentation Principles
- Read existing docs and build files before writing.
- Write for maintainers and users who need to do real tasks.
- Prefer executable commands over vague descriptions.
- Keep architecture docs tied to actual modules and data flow.
- Mark assumptions and unknowns instead of inventing details.

## Recommended Structure
```text
docs/
├── index.md
├── dev-setup.md
├── build-and-test.md
├── architecture.md
├── api.md
├── release.md
└── images/
```

Adjust names to match existing project conventions.

## MkDocs Setup
For a new project, use:
- `mkdocs.yml` at repository root.
- `docs/` as documentation source.
- MkDocs Material when a polished site is wanted.
- Clear `nav` entries for the main workflows.

Common commands:

```sh
python -m pip install mkdocs mkdocs-material
mkdocs serve
mkdocs build
```

Use project-specific dependency management if already present.

## README Content
Include:
- Project purpose.
- Quick start.
- Prerequisites.
- Build/test commands.
- Directory overview.
- Links to docs pages.

Do not duplicate every detail from docs; link to deeper pages.

## Dev Setup Page
Include:
- OS-specific prerequisites.
- Tool versions when known.
- Dependency installation.
- IDE/editor setup if relevant.
- Troubleshooting for common setup failures.

## Build and Test Page
Include:
- Configure/build/test commands.
- Presets or package scripts.
- Coverage commands if supported.
- Packaging and artifact locations.
- Expected output locations.

## Architecture Page
Include:
- Context and goals.
- Module responsibilities.
- Data flow or sequence flow.
- Public interfaces.
- Constraints and tradeoffs.
- Extension points.

## Diagrams
- Use Mermaid when diagrams are simple and maintainable.
- Store generated images only when the publishing path requires it.
- Keep diagrams close to the docs they explain.

## Publishing
- Document how docs are built in CI.
- If versioned docs are required, consider `mike` and record release workflow.
- Keep publishing credentials and secrets out of docs.

## Done Criteria
- Docs build successfully.
- Navigation points to existing files.
- Commands match the actual project.
- The user can onboard or perform the documented task from the page.
