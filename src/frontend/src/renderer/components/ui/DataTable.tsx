import { useState } from 'react';
import { motion } from 'framer-motion';
import { ChevronDown, ChevronUp, ChevronsUpDown, Search } from 'lucide-react';

interface IColumn<T> {
  key: string;
  label: string;
  sortable?: boolean;
  render?: (item: T) => React.ReactNode;
  width?: string;
}

interface IDataTableProps<T> {
  columns: IColumn<T>[];
  data: T[];
  keyExtractor: (item: T) => string;
  onRowClick?: (item: T) => void;
  searchable?: boolean;
  searchKeys?: string[];
  pageSize?: number;
}

export function DataTable<T extends Record<string, unknown>>({
  columns, data, keyExtractor, onRowClick, searchable, searchKeys, pageSize = 10,
}: IDataTableProps<T>) {
  const [sortKey, setSortKey] = useState<string | null>(null);
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);

  const handleSort = (key: string) => {
    if (sortKey === key) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('asc');
    }
  };

  let filtered = data;
  if (search && searchKeys) {
    const q = search.toLowerCase();
    filtered = data.filter((item) =>
      searchKeys.some((k) => String(item[k] ?? '').toLowerCase().includes(q))
    );
  }

  if (sortKey) {
    filtered = [...filtered].sort((a, b) => {
      const av = a[sortKey];
      const bv = b[sortKey];
      if (typeof av === 'number' && typeof bv === 'number') {
        return sortDir === 'asc' ? av - bv : bv - av;
      }
      const as = String(av ?? '');
      const bs = String(bv ?? '');
      const cmp = as.localeCompare(bs);
      return sortDir === 'asc' ? cmp : -cmp;
    });
  }

  const totalPages = Math.ceil(filtered.length / pageSize);
  const paged = filtered.slice(page * pageSize, (page + 1) * pageSize);

  return (
    <div className="glass rounded-xl overflow-hidden border border-white/5">
      {searchable && (
        <div className="flex items-center gap-2 px-4 py-3 border-b border-white/5">
          <Search size={14} className="text-white/20" />
          <input
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(0); }}
            placeholder="Search..."
            className="flex-1 bg-transparent text-xs text-white/60 placeholder-white/20 outline-none"
          />
          {search && (
            <span className="text-[10px] text-white/20">{filtered.length} results</span>
          )}
        </div>
      )}

      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <div className="w-10 h-10 rounded-xl bg-white/5 flex items-center justify-center mb-3">
            <Search size={18} className="text-white/20" />
          </div>
          <p className="text-sm text-white/30">{search ? 'No matching results' : 'No data available'}</p>
          <p className="text-[10px] text-white/20 mt-1">{search ? 'Try a different search term' : 'There is nothing to display yet'}</p>
        </div>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-white/5">
                  {columns.map((col) => (
                    <th
                      key={col.key}
                      onClick={col.sortable ? () => handleSort(col.key) : undefined}
                      className={`text-left text-[10px] uppercase tracking-wider text-white/30 font-semibold px-4 py-3 ${
                        col.sortable ? 'cursor-pointer hover:text-white/50' : ''
                      }`}
                      style={{ width: col.width }}
                    >
                      <div className="flex items-center gap-1">
                        {col.label}
                        {col.sortable && (
                          sortKey === col.key
                            ? (sortDir === 'asc' ? <ChevronUp size={12} /> : <ChevronDown size={12} />)
                            : <ChevronsUpDown size={12} className="opacity-30" />
                        )}
                      </div>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {paged.map((item, i) => (
                  <motion.tr
                    key={keyExtractor(item)}
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    transition={{ delay: i * 0.02 }}
                    onClick={() => onRowClick?.(item)}
                    className={`border-b border-white/5 last:border-0 transition-colors ${
                      onRowClick ? 'cursor-pointer hover:bg-white/[0.03]' : ''
                    }`}
                  >
                    {columns.map((col) => (
                      <td key={col.key} className="px-4 py-3 text-sm text-white/60">
                        {col.render ? col.render(item) : String(item[col.key] ?? '')}
                      </td>
                    ))}
                  </motion.tr>
                ))}
              </tbody>
            </table>
          </div>
          {totalPages > 1 && (
            <div className="flex items-center justify-between px-4 py-3 border-t border-white/5">
              <span className="text-[10px] text-white/20">
                Page {page + 1} of {totalPages}
              </span>
              <div className="flex gap-1">
                {Array.from({ length: totalPages }, (_, i) => (
                  <button
                    key={i}
                    onClick={() => setPage(i)}
                    className={`w-6 h-6 rounded text-[10px] font-medium transition-colors ${
                      i === page ? 'bg-primary-500/20 text-primary-300' : 'text-white/30 hover:text-white/60 hover:bg-white/5'
                    }`}
                  >
                    {i + 1}
                  </button>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
