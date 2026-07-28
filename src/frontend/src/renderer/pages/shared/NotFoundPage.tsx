import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Shield, Home } from 'lucide-react';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { GlassCard } from '../../components/ui/GlassCard';

export function NotFoundPage() {
  const navigate = useNavigate();

  return (
    <div className="h-full flex items-center justify-center">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
        <GlassCard className="p-12 flex flex-col items-center text-center max-w-md">
          <div className="w-16 h-16 rounded-2xl bg-rose-500/10 border border-rose-500/20 flex items-center justify-center mb-6">
            <Shield size={32} className="text-rose-400" />
          </div>
          <h1 className="text-5xl font-bold text-white mb-2">404</h1>
          <p className="text-lg text-white/60 mb-1">Page Not Found</p>
          <p className="text-sm text-white/30 mb-8">The page you're looking for doesn't exist or has been moved.</p>
          <AnimatedButton variant="primary" icon={<Home size={14} />} onClick={() => navigate('/dashboard')}>
            Back to Dashboard
          </AnimatedButton>
        </GlassCard>
      </motion.div>
    </div>
  );
}
