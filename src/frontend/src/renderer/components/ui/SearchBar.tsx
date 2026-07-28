import { useState } from 'react';
import { Search, X } from 'lucide-react';

interface ISearchBarProps {
  placeholder?: string;
  onSearch?: (query: string) => void;
  value?: string;
  onChange?: (value: string) => void;
  className?: string;
}

export function SearchBar({ placeholder = 'Search...', onSearch, value, onChange, className = '' }: ISearchBarProps) {
  const [internalValue, setInternalValue] = useState('');

  const isControlled = value !== undefined;
  const displayValue = isControlled ? value : internalValue;
  const setValue = isControlled ? onChange! : setInternalValue;

  const handleChange = (v: string) => {
    setValue(v);
    onSearch?.(v);
  };

  return (
    <div className={`relative ${className}`}>
      <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-white/20" />
      <input
        value={displayValue}
        onChange={(e) => handleChange(e.target.value)}
        placeholder={placeholder}
        className="w-full bg-white/5 border border-white/10 rounded-lg pl-9 pr-8 py-2 text-xs text-white/60 placeholder-white/20 outline-none focus:border-primary-500/30 focus:ring-1 focus:ring-primary-500/20 transition-all"
      />
      {displayValue && (
        <button
          onClick={() => handleChange('')}
          className="absolute right-3 top-1/2 -translate-y-1/2 text-white/20 hover:text-white/50"
        >
          <X size={12} />
        </button>
      )}
    </div>
  );
}
