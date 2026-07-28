import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Gavel, ShieldAlert, Clock, CheckCircle, XCircle } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { AnimatedModal } from '../../components/ui/AnimatedModal';
import { DataTable } from '../../components/ui/DataTable';
import { Select } from '../../components/ui/Select';
import { MetricCard } from '../../components/ui/MetricCard';
import { adminApi } from '../../services/admin';
import { useUIStore } from '../../stores/uiStore';
import type { IAdminBanEntry } from '../../services/admin';

export function BanCenterPage() {
  const [bans, setBans] = useState<IAdminBanEntry[]>([]);
  const [issueModalOpen, setIssueModalOpen] = useState(false);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedBan, setSelectedBan] = useState<IAdminBanEntry | null>(null);
  const [banForm, setBanForm] = useState({ username: '', reason: '', type: 'Temporary', duration: '7d' });
  const { addToast } = useUIStore();

  useEffect(() => {
    adminApi.getBans().then(({ data }) => setBans(data)).catch((err) => console.error('[BanCenterPage] failed to fetch', err));
  }, []);

  const active = bans.filter((b) => b.active).length;
  const expired = bans.filter((b) => !b.active).length;
  const pendingAppeals = bans.reduce((sum, b) => sum + b.appeals, 0);

  const handleIssueBan = async () => {
    try {
      const { data } = await adminApi.createBan({ player: banForm.username, reason: banForm.reason, type: banForm.type, duration: banForm.duration });
      setBans((prev) => [data, ...prev]);
      setIssueModalOpen(false);
      setBanForm({ username: '', reason: '', type: 'Temporary', duration: '7d' });
      addToast({ type: 'success', title: 'Ban Issued', message: `${banForm.username} has been banned` });
    } catch {
      addToast({ type: 'error', title: 'Failed to issue ban' });
    }
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Ban Center</h1>
          <p className="text-sm text-white/30 mt-0.5">Manage player bans and restrictions</p>
        </div>
        <AnimatedButton variant="gradient" icon={<Gavel size={14} />} onClick={() => setIssueModalOpen(true)}>Issue Ban</AnimatedButton>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Total Bans" value={String(bans.length)} icon={<ShieldAlert size={16} />} />
        <MetricCard title="Active" value={String(active)} trend="up" icon={<Clock size={16} />} />
        <MetricCard title="Expired" value={String(expired)} trend="down" icon={<CheckCircle size={16} />} />
        <MetricCard title="Appeals Pending" value={String(pendingAppeals)} trend="up" icon={<ShieldAlert size={16} />} />
      </div>

      <GlassCard className="p-6">
        <DataTable
          columns={[
            { key: 'player', label: 'Player', sortable: true },
            { key: 'reason', label: 'Reason', sortable: true },
            {
              key: 'type', label: 'Type', sortable: true,
              render: (item: IAdminBanEntry) => (
                <span className={`text-[10px] px-2 py-0.5 rounded-full ${item.type === 'Permanent' ? 'bg-rose-500/20 text-rose-400' : 'bg-amber-500/20 text-amber-400'}`}>{item.type}</span>
              ),
            },
            { key: 'issuedBy', label: 'Issued By' },
            { key: 'issuedAt', label: 'Date', sortable: true },
            {
              key: 'active', label: 'Status', sortable: true,
              render: (item: IAdminBanEntry) => (
                <span className={`text-[10px] ${item.active ? 'text-emerald-400' : 'text-white/30'}`}>{item.active ? 'Active' : 'Expired'}</span>
              ),
            },
            { key: 'appeals', label: 'Appeals' },
          ]}
          data={bans}
          keyExtractor={(item) => item.id}
          searchable
          searchKeys={['player', 'reason']}
          onRowClick={(item) => { setSelectedBan(item); setDetailModalOpen(true); }}
        />
      </GlassCard>

      <AnimatedModal open={issueModalOpen} onClose={() => setIssueModalOpen(false)} title="Issue Ban">
        <div className="space-y-4">
          <div>
            <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Username</label>
            <input value={banForm.username} onChange={(e) => setBanForm((f) => ({ ...f, username: e.target.value }))} placeholder="Player username" className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
          </div>
          <div>
            <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Reason</label>
            <input value={banForm.reason} onChange={(e) => setBanForm((f) => ({ ...f, reason: e.target.value }))} placeholder="Reason for ban" className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
          </div>
          <div className="flex gap-4">
            <div className="flex-1">
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Type</label>
              <Select value={banForm.type} onChange={(e) => setBanForm((f) => ({ ...f, type: e.target.value }))}>
                <option value="Temporary">Temporary</option>
                <option value="Permanent">Permanent</option>
              </Select>
            </div>
            <div className="flex-1">
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Duration</label>
              <Select value={banForm.duration} onChange={(e) => setBanForm((f) => ({ ...f, duration: e.target.value }))}>
                <option value="24h">24 Hours</option>
                <option value="7d">7 Days</option>
                <option value="30d">30 Days</option>
                <option value="permanent">Permanent</option>
              </Select>
            </div>
          </div>
          <div className="flex gap-2 pt-2">
            <AnimatedButton variant="secondary" onClick={() => setIssueModalOpen(false)} fullWidth>Cancel</AnimatedButton>
            <AnimatedButton variant="gradient" onClick={handleIssueBan} fullWidth>Issue Ban</AnimatedButton>
          </div>
        </div>
      </AnimatedModal>

      <AnimatedModal open={detailModalOpen} onClose={() => setDetailModalOpen(false)} title="Ban Details">
        {selectedBan && (
          <div className="space-y-3">
            <div className="flex items-center justify-between py-2 border-b border-white/5"><span className="text-xs text-white/40">Player</span><span className="text-xs text-white/70">{selectedBan.player}</span></div>
            <div className="flex items-center justify-between py-2 border-b border-white/5"><span className="text-xs text-white/40">Reason</span><span className="text-xs text-white/70">{selectedBan.reason}</span></div>
            <div className="flex items-center justify-between py-2 border-b border-white/5"><span className="text-xs text-white/40">Type</span><span className="text-xs text-white/70">{selectedBan.type}</span></div>
            <div className="flex items-center justify-between py-2 border-b border-white/5"><span className="text-xs text-white/40">Issued By</span><span className="text-xs text-white/70">{selectedBan.issuedBy}</span></div>
            <div className="flex items-center justify-between py-2 border-b border-white/5"><span className="text-xs text-white/40">Date</span><span className="text-xs text-white/70">{selectedBan.issuedAt}</span></div>
            <div className="flex items-center justify-between py-2 border-b border-white/5"><span className="text-xs text-white/40">Status</span><span className={`text-xs ${selectedBan.active ? 'text-emerald-400' : 'text-white/30'}`}>{selectedBan.active ? 'Active' : 'Expired'}</span></div>
            <div className="flex items-center justify-between py-2"><span className="text-xs text-white/40">Appeals</span><span className="text-xs text-white/70">{selectedBan.appeals}</span></div>
            <div className="flex gap-2 pt-2">
              <AnimatedButton variant="secondary" onClick={async () => {
                try { await adminApi.revokeBan(selectedBan.id); setBans((prev) => prev.map((b) => b.id === selectedBan.id ? { ...b, active: false } : b)); setDetailModalOpen(false); addToast({ type: 'info', title: 'Ban Revoked' }); } catch { addToast({ type: 'error', title: 'Failed to revoke ban' }); }
              }} icon={<XCircle size={12} />} fullWidth disabled={!selectedBan.active}>Revoke Ban</AnimatedButton>
              <AnimatedButton variant="gradient" onClick={() => setDetailModalOpen(false)} fullWidth>Close</AnimatedButton>
            </div>
          </div>
        )}
      </AnimatedModal>
    </div>
  );
}
