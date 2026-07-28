import { useEffect, useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft, Shield, AlertTriangle, Clock, Monitor, Globe, Fingerprint, Copy, Check, Activity, Flag, Bug, HelpCircle, MessageSquare } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { DataTable } from '../../components/ui/DataTable';
import { moderatorApi } from '../../services/moderator';
import { reportApi } from '../../services/reports';
import { useUIStore } from '../../stores/uiStore';
import type { IPlayerDetail } from '../../services/moderator';
import type { IPlayerReport } from '../../services/reports';

const severityColors: Record<string, string> = { critical: 'bg-rose-500/20 text-rose-400', high: 'bg-amber-500/20 text-amber-400', medium: 'bg-primary-500/20 text-primary-300', low: 'bg-white/5 text-white/30' };

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  const handleCopy = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {}
  }, [text]);
  return (
    <button onClick={handleCopy} className="text-white/20 hover:text-primary-400 transition-all shrink-0" title="Click to copy">
      {copied ? <Check size={12} className="text-emerald-400" /> : <Copy size={12} />}
    </button>
  );
}

export function PlayerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { addToast } = useUIStore();
  const [player, setPlayer] = useState<IPlayerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [playerReports, setPlayerReports] = useState<IPlayerReport[]>([]);
  const [showReports, setShowReports] = useState(false);
  const [flagModalOpen, setFlagModalOpen] = useState(false);
  const [flagReason, setFlagReason] = useState('');
  const [flagDescription, setFlagDescription] = useState('');
  const [flagging, setFlagging] = useState(false);

  const fetchPlayer = useCallback(() => {
    if (!id) return;
    moderatorApi.getPlayerDetail(id)
      .then(({ data }) => setPlayer(data))
      .catch(() => { if (!player) addToast({ type: 'error', title: 'Failed to load player details' }); })
      .finally(() => setLoading(false));
    moderatorApi.getPlayerReports(id)
      .then(({ data }) => setPlayerReports(data))
      .catch(() => {});
  }, [id]);

  useEffect(() => { fetchPlayer(); }, [fetchPlayer]);

  useEffect(() => {
    if (!id) return;
    const interval = setInterval(() => {
      moderatorApi.getPlayerDetail(id).then(({ data }) => setPlayer(data)).catch(() => {});
    }, 10000);
    return () => clearInterval(interval);
  }, [id]);

  const handleFlagPlayer = async () => {
    if (!player || !flagReason) return;
    setFlagging(true);
    try {
      await reportApi.submitReport({
        ticketType: 'report_player',
        playerName: player.username,
        reason: flagReason,
        description: flagDescription || `Flagged by moderator for review`,
      });
      addToast({ type: 'success', title: 'Player Flagged', message: `${player.username} has been flagged for review` });
      setFlagModalOpen(false);
      setFlagReason('');
      setFlagDescription('');
    } catch {
      addToast({ type: 'error', title: 'Failed to flag player' });
    } finally {
      setFlagging(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-white/5 rounded-lg animate-pulse" />
        <div className="h-48 bg-white/5 rounded-xl animate-pulse" />
        <div className="h-64 bg-white/5 rounded-xl animate-pulse" />
      </div>
    );
  }

  if (!player) {
    return (
      <div className="flex flex-col items-center justify-center py-24">
        <AlertTriangle size={48} className="text-white/10 mb-4" />
        <p className="text-white/30 text-sm">Player not found</p>
        <AnimatedButton variant="secondary" onClick={() => navigate('/moderator/players')} className="mt-4">Back to Search</AnimatedButton>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-6xl">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <button onClick={() => navigate('/moderator/players')} className="flex items-center gap-1.5 text-xs text-white/30 hover:text-white/60 mb-4 transition-all">
          <ArrowLeft size={14} /> Back to Player Search
        </button>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center text-lg font-bold text-white">
              {player.username.charAt(0)}
            </div>
            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-bold text-white">{player.username}</h1>
                <div className={`w-2.5 h-2.5 rounded-full animate-pulse ${player.status === 'online' ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.6)]' : player.status === 'suspected' ? 'bg-amber-500' : 'bg-white/20'}`} />
                <span className="text-xs text-white/40 capitalize">{player.status}</span>
              </div>
              <p className="text-sm text-white/30 mt-0.5">{player.gameName || 'Unknown Game'}</p>
            </div>
          </div>
        </div>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <GlassCard className="text-center py-4"><Shield size={16} className="mx-auto mb-1 text-primary-400" /><p className="text-lg font-bold text-white">{player.trustScore}%</p><p className="text-[10px] text-white/30">Trust Score</p></GlassCard>
        <GlassCard className="text-center py-4"><Clock size={16} className="mx-auto mb-1 text-white/30" /><p className="text-lg font-bold text-white">{player.hoursPlayed}h</p><p className="text-[10px] text-white/30">Hours Played</p></GlassCard>
        <GlassCard className="text-center py-4"><AlertTriangle size={16} className="mx-auto mb-1 text-amber-400" /><p className="text-lg font-bold text-white">{player.reportsCount}</p><p className="text-[10px] text-white/30">Reports</p></GlassCard>
        <GlassCard className="text-center py-4"><AlertTriangle size={16} className="mx-auto mb-1 text-rose-400" /><p className="text-lg font-bold text-white">{player.bansCount}</p><p className="text-[10px] text-white/30">Bans</p></GlassCard>
      </div>

      <GlassCard className="p-6">
        <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Identity & Fingerprint</h3>
        <div className="grid grid-cols-3 gap-4">
          {player.ipAddress && (
            <div className="p-3 rounded-lg bg-white/[0.03] border border-white/5 space-y-1">
              <div className="flex items-center gap-1.5 text-[10px] text-white/30 uppercase tracking-wider"><Globe size={12} />IP Address</div>
              <div className="flex items-center gap-2">
                <p className="text-xs font-mono text-white/70 truncate">{player.ipAddress}</p>
                <CopyButton text={player.ipAddress} />
              </div>
            </div>
          )}
          {player.hardwareId && (
            <div className="p-3 rounded-lg bg-white/[0.03] border border-white/5 space-y-1">
              <div className="flex items-center gap-1.5 text-[10px] text-white/30 uppercase tracking-wider"><Monitor size={12} />Hardware ID</div>
              <div className="flex items-center gap-2">
                <p className="text-xs font-mono text-white/70 break-all truncate">{player.hardwareId}</p>
                <CopyButton text={player.hardwareId} />
              </div>
            </div>
          )}
          {player.serialNumber && (
            <div className="p-3 rounded-lg bg-white/[0.03] border border-white/5 space-y-1">
              <div className="flex items-center gap-1.5 text-[10px] text-white/30 uppercase tracking-wider"><Fingerprint size={12} />Serial Number</div>
              <div className="flex items-center gap-2">
                <p className="text-xs font-mono text-white/70 break-all truncate">{player.serialNumber}</p>
                <CopyButton text={player.serialNumber} />
              </div>
            </div>
          )}
          {!player.ipAddress && !player.hardwareId && !player.serialNumber && (
            <p className="text-xs text-white/20 col-span-3">No identity data available</p>
          )}
        </div>
      </GlassCard>

      {player.detections && player.detections.length > 0 && (
        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Detection History</h3>
          <DataTable
            columns={[
              { key: 'type', label: 'Type', sortable: true },
              { key: 'description', label: 'Description' },
              {
                key: 'severity', label: 'Severity', sortable: true,
                render: (item: Record<string, unknown>) => <span className={`text-[10px] px-2 py-0.5 rounded-full ${severityColors[item.severity as string] || severityColors.low}`}>{item.severity as string}</span>,
              },
              { key: 'confidence', label: 'Confidence', sortable: true, render: (item: Record<string, unknown>) => <span className="text-xs font-mono text-white/60">{String(item.confidence)}%</span> },
              { key: 'timestamp', label: 'Time', sortable: true, render: (item: Record<string, unknown>) => <span className="text-xs text-white/40">{new Date(item.timestamp as string).toLocaleString()}</span> },
            ]}
            data={player.detections as unknown as Record<string, unknown>[]}
            keyExtractor={(item: Record<string, unknown>) => item.id as string}
          />
        </GlassCard>
      )}

      {player.sessions && player.sessions.length > 0 && (
        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Session History</h3>
          <DataTable
            columns={[
              { key: 'ipAddress', label: 'IP Address', render: (item: Record<string, unknown>) => <span className="text-xs font-mono text-white/60">{item.ipAddress as string}</span> },
              { key: 'deviceId', label: 'Device ID', render: (item: Record<string, unknown>) => <span className="text-xs font-mono text-white/60 break-all max-w-[200px] block truncate">{item.deviceId as string}</span> },
              { key: 'createdAt', label: 'Created', sortable: true, render: (item: Record<string, unknown>) => <span className="text-xs text-white/40">{new Date(item.createdAt as string).toLocaleString()}</span> },
              { key: 'isActive', label: 'Active', render: (item: Record<string, unknown>) => <span className={`text-[10px] ${item.isActive ? 'text-emerald-400' : 'text-white/30'}`}>{item.isActive ? 'Active' : 'Inactive'}</span> },
            ]}
            data={player.sessions as unknown as Record<string, unknown>[]}
            keyExtractor={(item: Record<string, unknown>) => item.id as string}
          />
        </GlassCard>
      )}

      {playerReports.length > 0 && (
        <GlassCard className="p-6">
          <button onClick={() => setShowReports(!showReports)} className="flex items-center justify-between w-full">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Reports ({playerReports.length})</h3>
            <span className={`text-white/30 transition-transform ${showReports ? 'rotate-180' : ''}`}>
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="6 9 12 15 18 9" /></svg>
            </span>
          </button>
          {showReports && (
            <div className="mt-4 space-y-2">
              {playerReports.map((r) => (
                <div key={r.id} className="flex items-center justify-between p-3 rounded-lg bg-white/[0.02] border border-white/5">
                  <div className="flex items-center gap-2">
                    {r.ticketType === 'report_player' ? <Flag size={12} className="text-rose-400" />
                      : r.ticketType === 'bug' ? <Bug size={12} className="text-amber-400" />
                      : r.ticketType === 'help' ? <HelpCircle size={12} className="text-primary-400" />
                      : <MessageSquare size={12} className="text-white/30" />}
                    <div>
                      <p className="text-xs text-white/60">{r.reason}</p>
                      <p className="text-[10px] text-white/30">by {r.playerName || 'Unknown'} &middot; {new Date(r.createdAt).toLocaleDateString()}</p>
                    </div>
                  </div>
                  <span className={`text-[10px] px-2 py-0.5 rounded-full ${
                    r.status === 'resolved' ? 'bg-emerald-500/20 text-emerald-400'
                    : r.status === 'investigating' ? 'bg-amber-500/20 text-amber-400'
                    : r.status === 'dismissed' ? 'bg-rose-500/20 text-rose-400'
                    : 'bg-white/5 text-white/30'
                  }`}>{r.status}</span>
                </div>
              ))}
            </div>
          )}
        </GlassCard>
      )}

      <div className="flex justify-end gap-2">
        <AnimatedButton variant="secondary" icon={<AlertTriangle size={12} />} onClick={() => setFlagModalOpen(true)}>Flag Player</AnimatedButton>
        <AnimatedButton variant="gradient" icon={<Shield size={12} />} onClick={() => navigate('/moderator/reports?player=' + encodeURIComponent(player.username))}>View Reports</AnimatedButton>
      </div>

      {/* Flag Player Modal */}
      {flagModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={() => !flagging && setFlagModalOpen(false)}>
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="bg-[#1a1f2e] border border-white/10 rounded-2xl p-6 w-full max-w-md shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-sm font-semibold text-white mb-4">Flag {player.username} for Review</h3>
            <div className="space-y-3">
              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Reason *</label>
                <select
                  value={flagReason}
                  onChange={(e) => setFlagReason(e.target.value)}
                  className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/40"
                  style={{ colorScheme: 'dark' }}
                >
                  <option value="" style={{ background: '#1e293b', color: '#94a3b8' }}>Select a reason...</option>
                  <option value="Speed hack" style={{ background: '#1e293b', color: '#e2e8f0' }}>Speed hack</option>
                  <option value="Aimbot / Wallhack" style={{ background: '#1e293b', color: '#e2e8f0' }}>Aimbot / Wallhack</option>
                  <option value="Memory injection" style={{ background: '#1e293b', color: '#e2e8f0' }}>Memory injection</option>
                  <option value="Script abuse" style={{ background: '#1e293b', color: '#e2e8f0' }}>Script abuse</option>
                  <option value="Harassment" style={{ background: '#1e293b', color: '#e2e8f0' }}>Harassment</option>
                  <option value="Other" style={{ background: '#1e293b', color: '#e2e8f0' }}>Other</option>
                </select>
              </div>
              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Details (optional)</label>
                <textarea
                  value={flagDescription}
                  onChange={(e) => setFlagDescription(e.target.value)}
                  rows={3}
                  className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 resize-none"
                  placeholder="Additional context..."
                />
              </div>
            </div>
            <div className="flex gap-2 mt-4">
              <AnimatedButton variant="secondary" onClick={() => setFlagModalOpen(false)} disabled={flagging} fullWidth>Cancel</AnimatedButton>
              <AnimatedButton variant="gradient" icon={<Flag size={12} />} onClick={handleFlagPlayer} disabled={!flagReason} loading={flagging} fullWidth>Flag Player</AnimatedButton>
            </div>
          </motion.div>
        </div>
      )}
    </div>
  );
}
