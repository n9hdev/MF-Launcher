import { motion } from 'framer-motion';
import { Shield } from 'lucide-react';

interface ITrustScoreProps {
  score: number;
  size?: 'sm' | 'md' | 'lg';
  showLabel?: boolean;
  animated?: boolean;
}

const sizeConfig = {
  sm: { ring: 40, stroke: 4, fontSize: 'text-[10px]', iconSize: 10 },
  md: { ring: 64, stroke: 5, fontSize: 'text-sm', iconSize: 14 },
  lg: { ring: 96, stroke: 6, fontSize: 'text-lg', iconSize: 18 },
};

export function TrustScore({ score, size = 'md', showLabel = true, animated = true }: ITrustScoreProps) {
  const cfg = sizeConfig[size];
  const radius = (cfg.ring - cfg.stroke) / 2;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (score / 100) * circumference;

  const color = score > 70 ? '#22c55e' : score > 40 ? '#f59e0b' : '#ef4444';
  const label = score > 70 ? 'Good' : score > 40 ? 'Fair' : 'Poor';

  return (
    <div className="flex flex-col items-center gap-1">
      <div className="relative" style={{ width: cfg.ring, height: cfg.ring }}>
        <svg width={cfg.ring} height={cfg.ring} className="transform -rotate-90">
          <circle
            cx={cfg.ring / 2}
            cy={cfg.ring / 2}
            r={radius}
            fill="none"
            stroke="rgba(255,255,255,0.05)"
            strokeWidth={cfg.stroke}
          />
          <motion.circle
            cx={cfg.ring / 2}
            cy={cfg.ring / 2}
            r={radius}
            fill="none"
            stroke={color}
            strokeWidth={cfg.stroke}
            strokeLinecap="round"
            strokeDasharray={circumference}
            initial={animated ? { strokeDashoffset: circumference } : undefined}
            animate={{ strokeDashoffset: offset }}
            transition={{ duration: 1.5, ease: 'easeOut' }}
          />
        </svg>
        <div className="absolute inset-0 flex items-center justify-center">
          <span className={`${cfg.fontSize} font-bold`} style={{ color }}>{score}%</span>
        </div>
      </div>
      {showLabel && (
        <div className="flex items-center gap-1">
          <Shield size={10} style={{ color }} />
          <span className="text-[10px] font-medium" style={{ color }}>{label}</span>
        </div>
      )}
    </div>
  );
}
