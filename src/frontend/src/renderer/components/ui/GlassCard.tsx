import { motion } from 'framer-motion';
import type { ReactNode } from 'react';

interface IGlassCardProps {
  children: ReactNode;
  className?: string;
  glow?: 'none' | 'primary' | 'success' | 'error' | 'warning';
  onClick?: () => void;
  hover?: boolean;
  padding?: 'sm' | 'md' | 'lg';
}

const glowMap = {
  none: '',
  primary: 'glow-primary',
  success: 'glow-success',
  error: 'glow-error',
  warning: 'glow-warning',
};

const paddingMap = {
  sm: 'p-4',
  md: 'p-5',
  lg: 'p-6',
};

export function GlassCard({ children, className = '', glow = 'none', onClick, hover = true, padding = 'md' }: IGlassCardProps) {
  const Comp = motion[onClick ? 'button' : 'div'] as typeof motion.div;

  return (
    <Comp
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ type: 'spring', stiffness: 300, damping: 25 }}
      className={`glass rounded-xl ${paddingMap[padding]} ${glowMap[glow]} ${hover ? 'glass-hover' : ''} ${onClick ? 'cursor-pointer text-left w-full' : ''} ${className}`}
      onClick={onClick}
      whileHover={onClick ? { scale: 1.01, y: -2 } : hover ? { y: -2 } : undefined}
    >
      {children}
    </Comp>
  );
}
