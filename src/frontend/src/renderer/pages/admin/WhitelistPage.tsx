import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { UserCheck, Shield, Plus } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { DataTable } from '../../components/ui/DataTable';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { AnimatedModal } from '../../components/ui/AnimatedModal';
import { Select } from '../../components/ui/Select';
import { MetricCard } from '../../components/ui/MetricCard';
import { adminApi } from '../../services/admin';
import { useUIStore } from '../../stores/uiStore';
import type { IWhitelistEntry } from '../../services/admin';

export function WhitelistPage() {
  const [entries, setEntries] = useState<IWhitelistEntry[]>([]);
  const [addModalOpen, setAddModalOpen] = useState(false);
  const [addForm, setAddForm] = useState({ entry: '', type: 'Process', reason: '' });

  const { addToast } = useUIStore();

  useEffect(() => {
    adminApi.getWhitelist().then(({ data }) => setEntries(data)).catch((err) => console.error('[WhitelistPage] failed to fetch', err));
  }, []);

  const handleAddEntry = async () => {
    try {
      const { data } = await adminApi.addWhitelistEntry({ entry: addForm.entry, type: addForm.type, reason: addForm.reason });
      setEntries((prev) => [data, ...prev]);
      setAddModalOpen(false);
      setAddForm({ entry: '', type: 'Process', reason: '' });
      addToast({ type: 'success', title: 'Entry Added', message: `"${addForm.entry}" whitelisted` });
    } catch {
      addToast({ type: 'error', title: 'Failed to add whitelist entry' });
    }
  };

  const processes = entries.filter((e) => e.type === 'Process').length;
  const paths = entries.filter((e) => e.type === 'Path').length;

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Whitelist Management</h1>
          <p className="text-sm text-white/30 mt-0.5">Manage trusted processes and paths</p>
        </div>
        <AnimatedButton variant="gradient" icon={<Plus size={14} />} onClick={() => setAddModalOpen(true)}>Add Entry</AnimatedButton>
      </motion.div>

      <div className="grid grid-cols-3 gap-4">
        <MetricCard title="Whitelisted Entries" value={String(entries.length)} icon={<UserCheck size={16} />} />
        <MetricCard title="Processes" value={String(processes)} icon={<Shield size={16} />} />
        <MetricCard title="Paths" value={String(paths)} icon={<Shield size={16} />} />
      </div>

      <GlassCard className="p-6">
        <DataTable
          columns={[
            { key: 'entry', label: 'Entry', sortable: true },
            { key: 'type', label: 'Type', sortable: true },
            { key: 'addedBy', label: 'Added By' },
            { key: 'addedAt', label: 'Date', sortable: true },
            { key: 'reason', label: 'Reason' },
          ]}
          data={entries}
          keyExtractor={(item) => item.id}
          searchable
          searchKeys={['entry', 'reason']}
        />
      </GlassCard>

      <AnimatedModal open={addModalOpen} onClose={() => setAddModalOpen(false)} title="Add Whitelist Entry">
        <div className="space-y-4">
          <div>
            <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Entry</label>
            <input value={addForm.entry} onChange={(e) => setAddForm((f) => ({ ...f, entry: e.target.value }))} placeholder="Process name or file path" className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
          </div>
          <div>
            <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Type</label>
            <Select value={addForm.type} onChange={(e) => setAddForm((f) => ({ ...f, type: e.target.value }))}>
              <option value="Process">Process</option>
              <option value="Path">Path</option>
            </Select>
          </div>
          <div>
            <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Reason</label>
            <input value={addForm.reason} onChange={(e) => setAddForm((f) => ({ ...f, reason: e.target.value }))} placeholder="Reason for whitelisting" className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
          </div>
          <div className="flex gap-2 pt-2">
            <AnimatedButton variant="secondary" onClick={() => setAddModalOpen(false)} fullWidth>Cancel</AnimatedButton>
            <AnimatedButton variant="gradient" onClick={handleAddEntry} fullWidth>Add Entry</AnimatedButton>
          </div>
        </div>
      </AnimatedModal>
    </div>
  );
}
