import { type ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import type { UserRole } from '../../types/global';

interface Props {
  children: ReactNode;
  roles?: UserRole[];
  fallbackPath?: string;
}

export function ProtectedRoute({ children, roles, fallbackPath = '/dashboard' }: Props) {
  const { user } = useAuthStore();

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (roles && roles.length > 0 && !roles.includes(user.role)) {
    return <Navigate to="/forbidden" replace />;
  }

  return <>{children}</>;
}
