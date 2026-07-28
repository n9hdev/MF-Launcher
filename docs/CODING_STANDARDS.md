# CODING STANDARDS — Mafia City Anti-Cheat V6

## General Principles

1. **No placeholders** — every function must do something meaningful
2. **No TODO comments** — finish features before committing
3. **No dead code** — remove commented-out code immediately
4. **No debug console.log** in production code
5. **Every feature = implemented + tested + documented**

## TypeScript / React Standards

### Naming
- Files: `PascalCase.tsx` for components, `camelCase.ts` for utilities
- Components: `PascalCase`
- Functions: `camelCase`
- Interfaces: `I` prefix (e.g., `IUser`)
- Types: `T` prefix (e.g., `TLoginResponse`)
- Enums: `PascalCase`
- Constants: `UPPER_SNAKE_CASE`

### Imports (ordered)
1. Node built-ins
2. Third-party packages
3. Absolute `@/` imports
4. Relative imports
5. CSS/styles (last)

### React Component Structure
```tsx
// 1. Imports
// 2. Types/Interfaces
// 3. Component function
// 4. Sub-components (if complex)
// 5. Styles (if any, else Tailwind)

export const ComponentName: React.FC<Props> = ({ prop1, prop2 }) => {
  // Hooks first
  // Event handlers
  // Render
};
```

### State Management (Zustand)
- One store per domain (auth, detection, settings)
- Actions are functions, not dispatched
- Selectors for derived state

## C# / .NET Standards

### Naming
- Classes: `PascalCase`
- Interfaces: `I` prefix (e.g., `IDetector`)
- Methods: `PascalCase`
- Properties: `PascalCase`
- Private fields: `_camelCase`
- Constants: `PascalCase`
- Async methods: `VerbAsync` suffix

### Project Structure
```
ProjectName/
├── Interfaces/
├── Services/
├── Models/
├── Configuration/
├── Extensions/
└── Exceptions/
```

### Dependency Injection
- Register all services in composition root
- Use constructor injection exclusively
- Avoid service locator pattern
- One interface per implementation

### Error Handling
- Use Result<T> pattern instead of exceptions for expected errors
- Exceptions only for unexpected errors
- Structured logging via Serilog (never Console.WriteLine)
- Global exception handler in middleware

### Async
- `async` all the way (no `Wait()` or `Result`)
- Cancellation tokens on all async methods
- Fire-and-forget only with explicit error logging

## Git Standards

### Commits
- `feat:` new feature
- `fix:` bug fix
- `docs:` documentation
- `refactor:` code change that fixes neither bug nor adds feature
- `test:` adding tests
- `chore:` build process, dependencies, etc.

### Branches
- `main` — production-ready
- `develop` — integration branch
- `feature/*` — new features
- `fix/*` — bug fixes
- `release/*` — release candidates

## Security Standards

1. Never hardcode secrets (API keys, tokens)
2. Use environment variables or encrypted config
3. Sanitize all user input
4. Validate all API responses server-side
5. Apply principle of least privilege
6. Log security events, not sensitive data
7. Use constant-time comparison for tokens
8. Rate limit API calls from client
