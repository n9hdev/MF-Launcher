import type { ReactNode } from 'react';
import { useFeatureFlag } from '../../hooks/useFeatureFlag';

interface IFeatureFlagGateProps {
  flag: string;
  children: ReactNode;
  fallback?: ReactNode;
}

export function FeatureFlagGate({ flag, children, fallback = null }: IFeatureFlagGateProps) {
  const enabled = useFeatureFlag(flag);
  return enabled ? <>{children}</> : <>{fallback}</>;
}
