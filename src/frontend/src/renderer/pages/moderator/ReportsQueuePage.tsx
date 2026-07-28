import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Search, Flag, Bug, HelpCircle, MessageSquare } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { DataTable } from '../../components/ui/DataTable';
import { SearchBar } from '../../components/ui/SearchBar';
import { moderatorApi } from '../../services/moderator';
import type { IPlayerReport } from '../../services/reports';

const statusTabs = ['All', 'Pending', 'Investigating', 'Resolved', 'Dismissed'];

export function ReportsQueuePage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const playerFilter = searchParams.get('player') || undefined;
  const [reports, setReports] = useState<IPlayerReport[]>([]);
  const [activeTab, setActiveTab] = useState('All');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    moderatorApi.getAllPlayerReports(playerFilter)
      .then(({ data }) => setReports(data))
      .catch((err) => console.error('[ReportsQueuePage] failed to fetch', err))
      .finally(() => setLoading(false));
  }, [playerFilter]);

  const filtered = (activeTab === 'All'
    ? reports
    : reports.filter((r) => r.status.toLowerCase() === activeTab.toLowerCase())
  ).filter((r) => !r.isFlagged);

  const statusIcon = (status: string) => {
    const colors: Record<string, string> = { resolved: 'text-emerald-400', investigating: 'text-amber-400', pending: 'text-white/30', dismissed: 'text-rose-400' };
    return <span className={`capitalize text-xs ${colors[status] || 'text-white/30'}`}>{status}</span>;
  };

  const ticketIcon = (type: string) => {
    switch (type) {
      case 'report_player': return <Flag size={12} className="text-rose-400" />;
      case 'bug': return <Bug size={12} className="text-amber-400" />;
      case 'help': return <HelpCircle size={12} className="text-primary-400" />;
      default: return <MessageSquare size={12} className="text-white/30" />;
    }
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-bold text-white">Reports Queue</h1>
            <p className="text-sm text-white/30 mt-0.5">Manage player-submitted tickets</p>
          </div>
          <SearchBar placeholder="Search tickets..." />
        </div>
      </motion.div>

      <GlassCard className="p-6">
        <div className="flex items-center gap-4 mb-6">
          {statusTabs.map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`text-xs px-3 py-1.5 rounded-lg transition-all ${
                activeTab === tab ? 'bg-primary-500/20 text-primary-300 border border-primary-500/30' : 'text-white/30 hover:text-white/50 border border-transparent'
              }`}
            >
              {tab}
            </button>
          ))}
        </div>

        <DataTable
          columns={[
            {
              key: 'ticketType',
              label: 'Type',
              sortable: true,
              render: (item: IPlayerReport) => (
                <div className="flex items-center gap-1.5">{ticketIcon(item.ticketType)}</div>
              ),
            },
            { key: 'playerName', label: 'Player / Ticket', sortable: true },
            { key: 'reason', label: 'Reason', sortable: true },
            {
              key: 'status',
              label: 'Status',
              sortable: true,
              render: (item: IPlayerReport) => statusIcon(item.status),
            },
            {
              key: 'createdAt',
              label: 'Created',
              sortable: true,
              render: (item: IPlayerReport) => (
                <span className="text-[10px] text-white/30">
                  {new Date(item.createdAt).toLocaleDateString()}
                </span>
              ),
            },
          ]}
          data={filtered}
          keyExtractor={(item) => item.id}
          onRowClick={(item) => navigate(`/moderator/reports/${item.id}`)}
        />
      </GlassCard>
    </div>
  );
}
