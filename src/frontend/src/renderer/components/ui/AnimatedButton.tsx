import { motion } from 'framer-motion';
import { Loader2, Lock } from 'lucide-react';
import type { ReactNode } from 'react';

interface IAnimatedButtonProps {
  children?: ReactNode;
  onClick?: () => void;
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'success' | 'gradient';
  size?: 'sm' | 'md' | 'lg';
  loading?: boolean;
  disabled?: boolean;
  locked?: boolean;
  lockedReason?: string;
  icon?: ReactNode;
  className?: string;
  fullWidth?: boolean;
  type?: 'button' | 'submit';
}

const variantStyles = {
  primary: 'bg-primary-600 hover:bg-primary-500 text-white shadow-lg shadow-primary-500/20 border border-primary-500/30',
  secondary: 'glass glass-hover text-white/80 border border-white/10',
  ghost: 'bg-transparent hover:bg-white/5 text-white/50',
  danger: 'bg-rose-600 hover:bg-rose-500 text-white shadow-lg shadow-rose-500/20 border border-rose-500/30',
  success: 'bg-emerald-600 hover:bg-emerald-500 text-white shadow-lg shadow-emerald-500/20 border border-emerald-500/30',
  gradient: 'bg-gradient-to-r from-primary-600 to-primary-500 hover:from-primary-500 hover:to-primary-400 text-white shadow-lg shadow-primary-500/25 border border-primary-400/30',
};

const sizeStyles = {
  sm: 'px-3 py-1.5 text-xs rounded-lg gap-1.5',
  md: 'px-4 py-2 text-sm rounded-lg gap-2',
  lg: 'px-6 py-3 text-base rounded-xl gap-2.5',
};

export function AnimatedButton({
  children, onClick, variant = 'primary', size = 'md', loading = false,
  disabled = false, locked = false, lockedReason, icon, className = '',
  fullWidth = false, type = 'button',
}: IAnimatedButtonProps) {
  const isInert = disabled || loading || locked;
  return (
    <motion.button
      type={type}
      whileHover={isInert ? undefined : { scale: 1.02, y: -1 }}
      whileTap={isInert ? undefined : { scale: 0.98 }}
      onClick={locked ? undefined : onClick}
      disabled={disabled || loading}
      title={locked ? lockedReason || 'Locked — complete requirements first' : undefined}
      className={`inline-flex items-center justify-center font-medium transition-all duration-200
        ${variantStyles[variant]} ${sizeStyles[size]} ${fullWidth ? 'w-full' : ''}
        ${isInert ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'} ${locked ? 'ring-1 ring-amber-500/30' : ''} ${className}`}
    >
      {loading ? <Loader2 size={size === 'lg' ? 18 : size === 'sm' ? 12 : 14} className="animate-spin" /> : locked ? <Lock size={size === 'lg' ? 18 : size === 'sm' ? 12 : 14} /> : icon}
      {children}
    </motion.button>
  );
}
