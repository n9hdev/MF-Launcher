import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { FileSearch, Filter, Download, X } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { DataTable } from '../../components/ui/DataTable';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { superAdminApi } from '../../services/superadmin';
import { useUIStore } from '../../stores/uiStore';
import type { IAuditLogEntry } from '../../services/superadmin';

export function AuditLogPage() {
  const [logs, setLogs] = useState<IAuditLogEntry[]>([]);
  const [filterOpen, setFilterOpen] = useState(false);
  const { addToast } = useUIStore();

  useEffect(() => {
    const fetch = () => superAdminApi.getAuditLogs().then(({ data }) => setLogs(data)).catch(() => {});
    fetch();
    const interval = setInterval(fetch, 30000);
    return () => clearInterval(interval);
  }, []);

  const handleExport = () => {
    const headers = ['Action', 'User', 'Target', 'Details', 'Timestamp', 'IP'];
    const csvRows = [headers.join(',')];
    logs.forEach((log) => {
      csvRows.push([log.action, log.user, log.target, `"${log.details}"`, log.timestamp, log.ip].join(','));
    });
    const blob = new Blob([csvRows.join('\n')], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = `audit-log-${Date.now()}.csv`; a.click();
    URL.revokeObjectURL(url);
    addToast({ type: 'success', title: 'Exported', message: `${logs.length} audit log entries exported as CSV` });
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Audit Log</h1>
          <p className="text-sm text-white/30 mt-0.5">Complete audit trail of all system actions</p>
        </div>
        <div className="flex gap-2">
          <AnimatedButton size="sm" variant="secondary" icon={<Filter size={12} />} onClick={() => setFilterOpen(!filterOpen)}>Filter</AnimatedButton>
          <AnimatedButton size="sm" variant="gradient" icon={<Download size={12} />} onClick={handleExport}>Export</AnimatedButton>
        </div>
      </motion.div>

      {filterOpen && (
        <GlassCard className="p-6">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Filter Options</h3>
            <button onClick={() => setFilterOpen(false)} className="text-white/30 hover:text-white/60"><X size={14} /></button>
          </div>
          <div className="grid grid-cols-4 gap-4">
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Action</label>
              <input placeholder="Filter by action..." className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">User</label>
              <input placeholder="Filter by user..." className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Date From</label>
              <input type="date" className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Date To</label>
              <input type="date" className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
          </div>
        </GlassCard>
      )}

      <GlassCard className="p-6">
        <DataTable
          columns={[
            { key: 'action', label: 'Action', sortable: true },
            { key: 'user', label: 'User', sortable: true },
            { key: 'target', label: 'Target', sortable: true },
            { key: 'details', label: 'Details' },
            { key: 'timestamp', label: 'Timestamp', sortable: true },
            { key: 'ip', label: 'IP Address' },
          ]}
          data={logs}
          keyExtractor={(r) => r.id}
          searchable
          searchKeys={['action', 'user', 'target', 'details']}
          pageSize={8}
        />
      </GlassCard>
    </div>
  );
}
