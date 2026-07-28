export const themeTokens = {
  colors: {
    primary: {
      50: '#eef2ff', 100: '#e0e7ff', 200: '#c7d2fe', 300: '#a5b4fc',
      400: '#818cf8', 500: '#6366f1', 600: '#4f46e5', 700: '#4338ca',
      800: '#3730a3', 900: '#312e81', 950: '#1e1b4b',
    },
    accent: {
      cyan: '#06b6d4',
      emerald: '#10b981',
      violet: '#8b5cf6',
      rose: '#f43f5e',
      amber: '#f59e0b',
    },
    surface: {
      50: '#f8fafc', 100: '#f1f5f9', 200: '#e2e8f0',
      700: '#1e293b', 800: '#0f172a', 900: '#020617', 950: '#000000',
    },
    glass: {
      light: 'rgba(255, 255, 255, 0.05)',
      medium: 'rgba(255, 255, 255, 0.08)',
      heavy: 'rgba(255, 255, 255, 0.12)',
      border: 'rgba(255, 255, 255, 0.06)',
      hover: 'rgba(255, 255, 255, 0.10)',
    },
    text: {
      primary: '#f1f5f9',
      secondary: '#94a3b8',
      tertiary: '#64748b',
      disabled: '#475569',
    },
    status: {
      success: '#22c55e',
      warning: '#f59e0b',
      error: '#ef4444',
      info: '#3b82f6',
      pending: '#a855f7',
    },
  },
  spacing: {
    xs: '4px', sm: '8px', md: '16px', lg: '24px', xl: '32px',
    '2xl': '48px', '3xl': '64px',
  },
  radius: {
    sm: '6px', md: '10px', lg: '16px', xl: '24px', full: '9999px',
  },
  animation: {
    fast: '150ms',
    normal: '250ms',
    slow: '400ms',
    spring: { type: 'spring' as const, stiffness: 300, damping: 25 },
    springSnap: { type: 'spring' as const, stiffness: 400, damping: 20 },
    springGentle: { type: 'spring' as const, stiffness: 200, damping: 30 },
  },
  typography: {
    fontFamily: "'Inter', system-ui, -apple-system, sans-serif",
    fontMono: "'JetBrains Mono', 'Fira Code', monospace",
    sizes: {
      xs: '0.75rem', sm: '0.8125rem', base: '0.875rem',
      lg: '1rem', xl: '1.25rem', '2xl': '1.5rem',
      '3xl': '1.875rem', '4xl': '2.25rem',
    },
    weights: {
      normal: '400', medium: '500', semibold: '600', bold: '700', extrabold: '800',
    },
  },
  shadows: {
    sm: '0 1px 2px rgba(0,0,0,0.3)',
    md: '0 4px 6px rgba(0,0,0,0.4)',
    lg: '0 10px 25px rgba(0,0,0,0.5)',
    xl: '0 20px 50px rgba(0,0,0,0.6)',
    glow: {
      primary: '0 0 15px rgba(99,102,241,0.3), 0 0 30px rgba(99,102,241,0.1)',
      success: '0 0 15px rgba(34,197,94,0.3), 0 0 30px rgba(34,197,94,0.1)',
      error: '0 0 15px rgba(239,68,68,0.3), 0 0 30px rgba(239,68,68,0.1)',
      warning: '0 0 15px rgba(245,158,11,0.3), 0 0 30px rgba(245,158,11,0.1)',
    },
  },
} as const;
