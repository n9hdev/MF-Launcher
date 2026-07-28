import { motion } from 'framer-motion';

interface IRiskGaugeProps {
  value: number; // 0-100
  label?: string;
  size?: 'sm' | 'md';
}

export function RiskGauge({ value, label, size = 'md' }: IRiskGaugeProps) {
  const height = size === 'sm' ? 4 : 6;
  const segments = 10;
  const segmentWidth = 100 / segments;

  const getColor = (v: number) => {
    if (v <= 30) return 'bg-emerald-500';
    if (v <= 60) return 'bg-amber-500';
    return 'bg-rose-500';
  };

  return (
    <div className="space-y-1.5">
      {label && (
        <div className="flex items-center justify-between">
          <span className="text-[10px] text-white/40">{label}</span>
          <span className="text-[10px] font-mono text-white/50">{value}%</span>
        </div>
      )}
      <div className="flex gap-1" style={{ height }}>
        {Array.from({ length: segments }, (_, i) => {
          const segmentVal = (i + 1) * 10;
          const filled = value >= segmentVal - 10;
          const partial = value > segmentVal - 10 && value < segmentVal;
          const fillPercent = partial ? ((value - (segmentVal - 10)) / 10) * 100 : 0;

          return (
            <div
              key={i}
              className="flex-1 rounded-full bg-white/5 overflow-hidden"
            >
              <motion.div
                initial={{ scaleX: 0 }}
                animate={{ scaleX: filled || partial ? 1 : 0 }}
                transition={{ duration: 0.3, delay: i * 0.05 }}
                className={`h-full rounded-full origin-left ${getColor(segmentVal)}`}
                style={{
                  opacity: filled ? 1 : partial ? 0.5 : 0,
                  transform: partial ? `scaleX(${fillPercent / 100})` : undefined,
                }}
              />
            </div>
          );
        })}
      </div>
    </div>
  );
}
