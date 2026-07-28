import { useEffect, useState, useCallback } from 'react';
import { motion } from 'framer-motion';
import { Clock, Shield, AlertTriangle, CheckCircle, Filter, X } from 'lucide-react';
import { historyApi } from '../../services/history';
import type { ITimelineEvent, IHistorySummary, IDetectionStats } from '../../services/history';
import { GlassCard } from '../../components/ui/GlassCard';
import { Select } from '../../components/ui/Select';
import { Timeline } from '../../components/ui/Timeline';
import { SearchBar } from '../../components/ui/SearchBar';

export function HistoryPage() {
  const [events, setEvents] = useState<ITimelineEvent[]>([]);
  const [summary, setSummary] = useState<IHistorySummary | null>(null);
  const [stats, setStats] = useState<IDetectionStats | null>(null);
  const [search, setSearch] = useState('');
  const [severity, setSeverity] = useState('');
  const [category, setCategory] = useState('');
  const [dateRange, setDateRange] = useState('');
  const [page, setPage] = useState(1);
  const [filterOpen, setFilterOpen] = useState(false);
  const [loading, setLoading] = useState(true);

  const fetchTimeline = useCallback(async (sev?: string, cat?: string, q?: string, p?: number) => {
    try {
      const params: Record<string, string | number> = {};
      if (sev) params.severity = sev;
      if (cat) params.category = cat;
      if (q) params.search = q;
      if (p && p > 1) params.page = p;
      const { data } = await historyApi.getTimeline(params);
      setEvents(data);
    } catch (err) {
      console.error('[HistoryPage] fetchTimeline failed', err);
    }
  }, []);

  useEffect(() => {
    Promise.all([
      historyApi.getSummary().then(({ data }) => setSummary(data)),
      historyApi.getStats().then(({ data }) => setStats(data)),
    ]).catch((err) => console.error('[HistoryPage] initial fetch failed', err)).finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    fetchTimeline(severity, category, search, page);
  }, [severity, category, search, page, fetchTimeline]);

  const handleSearch = (value: string) => {
    setSearch(value);
    setPage(1);
  };

  const handleSeverityChange = (value: string) => {
    setSeverity(value);
    setPage(1);
  };

  const handleCategoryChange = (value: string) => {
    setCategory(value);
    setPage(1);
  };

  const handleDateRangeChange = (value: string) => {
    setDateRange(value);
    setPage(1);
  };

  const severityColors: Record<string, string> = {
    critical: 'text-rose-400',
    high: 'text-rose-400',
    medium: 'text-amber-400',
    low: 'text-primary-400',
    info: 'text-white/40',
  };
  const severityBgs: Record<string, string> = {
    critical: 'bg-rose-500/20',
    high: 'bg-rose-500/10',
    medium: 'bg-amber-500/10',
    low: 'bg-primary-500/10',
    info: 'bg-white/5',
  };

  const summaryItems = summary ? [
    { label: 'Critical', count: summary.critical, color: severityColors.critical, bg: severityBgs.critical },
    { label: 'High', count: summary.high, color: severityColors.high, bg: severityBgs.high },
    { label: 'Medium', count: summary.medium, color: severityColors.medium, bg: severityBgs.medium },
    { label: 'Low', count: summary.low, color: severityColors.low, bg: severityBgs.low },
    { label: 'Info', count: summary.info, color: severityColors.info, bg: severityBgs.info },
  ] : [];

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-xl font-bold text-white">Detection History</h1>
        <p className="text-sm text-white/30 mt-0.5">Complete timeline of all security events</p>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Total Events', value: stats ? (stats.totalScans + stats.threatsFound).toLocaleString() : '...' },
          { label: 'Threats', value: stats?.threatsFound.toLocaleString() ?? '...' },
          { label: 'Clean Scans', value: stats?.cleanScans.toLocaleString() ?? '...' },
          { label: 'Uptime', value: stats ? `${stats.uptimePercent}%` : '...' },
        ].map((s) => (
          <GlassCard key={s.label} className="text-center py-4">
            <p className="text-lg font-bold text-white">{s.value}</p>
            <p className="text-[10px] text-white/30 mt-0.5">{s.label}</p>
          </GlassCard>
        ))}
      </div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2">
          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Event Timeline</h3>
              <div className="flex items-center gap-2">
                <SearchBar placeholder="Filter events..." className="w-48" value={search} onChange={handleSearch} />
                <button onClick={() => setFilterOpen(!filterOpen)} className="w-7 h-7 flex items-center justify-center rounded-lg glass glass-hover text-white/30">
                  {filterOpen ? <X size={12} /> : <Filter size={12} />}
                </button>
              </div>
            </div>
            {filterOpen && (
              <div className="mb-4 p-4 rounded-xl bg-white/[0.03] border border-white/5 grid grid-cols-3 gap-4">
                <div>
                  <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Severity</label>
                  <Select value={severity} onChange={(e) => handleSeverityChange(e.target.value)}>
                    <option value="">All</option>
                    <option value="critical">Critical</option>
                    <option value="high">High</option>
                    <option value="medium">Medium</option>
                    <option value="low">Low</option>
                    <option value="info">Info</option>
                  </Select>
                </div>
                <div>
                  <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Category</label>
                  <Select value={category} onChange={(e) => handleCategoryChange(e.target.value)}>
                    <option value="">All</option>
                    <option value="scan">Scan</option>
                    <option value="threat">Threat</option>
                    <option value="system">System</option>
                  </Select>
                </div>
                <div>
                  <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Date Range</label>
                  <Select value={dateRange} onChange={(e) => handleDateRangeChange(e.target.value)}>
                    <option value="">All Time</option>
                    <option value="today">Today</option>
                    <option value="7d">Last 7 Days</option>
                    <option value="30d">Last 30 Days</option>
                  </Select>
                </div>
              </div>
            )}
            {loading ? (
              <div className="space-y-3"><div className="h-12 bg-white/5 rounded-lg animate-pulse" /><div className="h-12 bg-white/5 rounded-lg animate-pulse" /><div className="h-12 bg-white/5 rounded-lg animate-pulse" /></div>
            ) : (
              <Timeline events={events.map((e) => ({
                id: e.id,
                type: e.severity === 'critical' || e.severity === 'high' ? 'warning' as const : e.severity === 'medium' ? 'info' as const : 'success' as const,
                title: e.title,
                description: e.description,
                timestamp: new Date(e.timestamp).toLocaleString(),
                severity: e.severity as 'low' | 'medium' | 'high',
                count: e.count,
              }))} />
            )}
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Event Summary</h3>
            {summary ? (
              <div className="space-y-3">
                {summaryItems.map((item) => (
                  <div key={item.label} className="flex items-center justify-between py-1.5">
                    <div className="flex items-center gap-2">
                      <div className={`w-2 h-2 rounded-full ${item.bg}`} />
                      <span className="text-xs text-white/50">{item.label}</span>
                    </div>
                    <span className={`text-xs font-mono ${item.color}`}>{item.count}</span>
                  </div>
                ))}
              </div>
            ) : (
              <div className="space-y-3"><div className="h-6 bg-white/5 rounded animate-pulse" /><div className="h-6 bg-white/5 rounded animate-pulse" /></div>
            )}
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
