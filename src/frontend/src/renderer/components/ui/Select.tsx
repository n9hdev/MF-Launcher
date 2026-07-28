import React, { useState, useRef, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown } from 'lucide-react';

interface ISelectProps {
  value?: string;
  onChange?: (e: React.ChangeEvent<HTMLSelectElement>) => void;
  children: React.ReactNode;
  className?: string;
  placeholder?: string;
  disabled?: boolean;
}

interface IOption {
  value: string;
  label: string;
}

function extractOptions(children: React.ReactNode): IOption[] {
  const options: IOption[] = [];
  React.Children.forEach(children, (child) => {
    if (React.isValidElement(child)) {
      if (child.type === 'option') {
        const { value, children: label } = child.props;
        const labelText = extractLabel(label);
        options.push({ value: String(value ?? labelText), label: labelText });
      } else if (child.type === React.Fragment) {
        options.push(...extractOptions(child.props.children));
      }
    }
  });
  return options;
}

function extractLabel(children: React.ReactNode): string {
  if (typeof children === 'string') return children;
  if (typeof children === 'number') return String(children);
  return '';
}

export function Select({ value, onChange, children, className = '', placeholder, disabled }: ISelectProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const options = extractOptions(children);
  const selected = options.find((o) => o.value === value);
  const display = selected?.label || placeholder || 'Select...';

  const handleSelect = useCallback((opt: IOption) => {
    if (onChange) {
      const event = { target: { value: opt.value } } as React.ChangeEvent<HTMLSelectElement>;
      onChange(event);
    }
    setOpen(false);
  }, [onChange]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  return (
    <div ref={ref} className={`relative ${className}`}>
      <button
        type="button"
        disabled={disabled}
        onClick={() => setOpen(!open)}
        className={`w-full flex items-center justify-between gap-2 bg-white/5 border ${open ? 'border-primary-500/30' : 'border-white/10'} rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none hover:border-white/20 transition-all disabled:opacity-50`}
      >
        <span className="truncate">{display}</span>
        <ChevronDown size={14} className={`text-white/20 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>
      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: -4, scaleY: 0.95 }}
            animate={{ opacity: 1, y: 0, scaleY: 1 }}
            exit={{ opacity: 0, y: -4, scaleY: 0.95 }}
            transition={{ duration: 0.15 }}
            className="absolute top-full left-0 right-0 mt-1 z-50 rounded-xl overflow-hidden origin-top"
            style={{ background: 'rgba(15, 23, 42, 0.98)', backdropFilter: 'blur(24px) saturate(1.6)', border: '1px solid rgba(255,255,255,0.08)' }}
          >
            {options.map((opt) => (
              <button
                key={opt.value}
                type="button"
                onClick={() => handleSelect(opt)}
                className={`w-full text-left px-4 py-2.5 text-xs transition-colors ${
                  opt.value === value ? 'text-primary-300 bg-primary-500/15' : 'text-white/60 hover:text-white/80 hover:bg-white/5'
                }`}
              >
                {opt.label}
              </button>
            ))}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
