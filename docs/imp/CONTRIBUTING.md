# Contributing

## Development Setup

See [INSTALL.md](INSTALL.md) for setup instructions.

## Code Style

### Backend (C#)
- File-scoped namespaces (`namespace X.Y;`)
- Implicit usings enabled
- Async methods suffixed with `Async`
- XML doc comments on public interfaces
- Follow existing patterns in `AntiCheat.Core/Services/`

### Frontend (TypeScript/React)
- Functional components with hooks
- Zustand for state management
- TailwindCSS for styling
- Framer Motion for animations
- Path aliases: `@/` → `src/renderer/`
- React.lazy() + Suspense for all route pages

## Testing

```bash
# Backend (xUnit + Moq + FluentAssertions)
dotnet test src/backend/AntiCheat.Tests

# Frontend (Vitest + happy-dom + testing-library)
cd src/frontend
npm test
```

- All tests must pass before merge
- New features require tests
- Bug fixes require a regression test

## Pull Request Process

1. Update ROADMAP.md if applicable
2. Update CHANGELOG.md with changes
3. Verify all 189 tests pass
4. Verify both backend and frontend build with 0 errors
5. No new TODO/FIXME/HACK comments

## Release Process

1. Complete Phase 16 checklist (see ROADMAP.md)
2. Tag with semantic version: `git tag v6.0.0-rc1`
3. Build Release configuration
4. Run full test suite
5. Generate installer
6. Publish release notes
