import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ShieldOff, Home } from 'lucide-react';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { GlassCard } from '../../components/ui/GlassCard';

export function ForbiddenPage() {
  const navigate = useNavigate();

  return (
    <div className="h-full flex items-center justify-center">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
        <GlassCard className="p-12 flex flex-col items-center text-center max-w-md">
          <div className="w-16 h-16 rounded-2xl bg-amber-500/10 border border-amber-500/20 flex items-center justify-center mb-6">
            <ShieldOff size={32} className="text-amber-400" />
          </div>
          <h1 className="text-5xl font-bold text-white mb-2">403</h1>
          <p className="text-lg text-white/60 mb-1">Access Denied</p>
          <p className="text-sm text-white/30 mb-8">You don't have permission to access this area.</p>
          <AnimatedButton variant="primary" icon={<Home size={14} />} onClick={() => navigate('/dashboard')}>
            Back to Dashboard
          </AnimatedButton>
        </GlassCard>
      </motion.div>
    </div>
  );
}
