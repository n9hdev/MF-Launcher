import { useEffect, useState, useCallback, useRef } from 'react';
import { motion } from 'framer-motion';
import { Monitor, Camera, Video, VideoOff, Users, Activity, Clock, Download, X, Play, Plus, Search } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import {
  screenCaptureApi,
  connectScreenStream,
  disconnectScreenStream,
  onFrameReceived,
  offFrameReceived,
  onStreamEnded,
  offStreamEnded,
  onStreamError,
  offStreamError,
  getScreenStreamConnection,
} from '../../services/screenCapture';
import { moderatorApi } from '../../services/moderator';
import type { IScreenFrame, IStreamSummary, IScreenshotCapture } from '../../services/screenCapture';
import type { IPlayerSearchResult } from '../../services/moderator';

export function LivePlayerView() {
  const [activeStreams, setActiveStreams] = useState<IStreamSummary[]>([]);
  const [selectedSessionId, setSelectedSessionId] = useState<string | null>(null);
  const [currentFrame, setCurrentFrame] = useState<IScreenFrame | null>(null);
  const [isViewing, setIsViewing] = useState(false);
  const [streamError, setStreamError] = useState<string | null>(null);
  const [screenshotHistory, setScreenshotHistory] = useState<IScreenshotCapture[]>([]);
  const [targetPlayer, setTargetPlayer] = useState('');
  const [fps, setFps] = useState(2);
  const [frameCount, setFrameCount] = useState(0);
  const [connectionStatus, setConnectionStatus] = useState<'disconnected' | 'connecting' | 'connected'>('disconnected');
  const [playerSearchResults, setPlayerSearchResults] = useState<IPlayerSearchResult[]>([]);
  const [searching, setSearching] = useState(false);
  const [showSearch, setShowSearch] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);

  const frameHandlerRef = useRef<((frame: IScreenFrame) => void) | null>(null);
  const endHandlerRef = useRef<(() => void) | null>(null);
  const errorHandlerRef = useRef<((msg: string) => void) | null>(null);

  useEffect(() => {
    screenCaptureApi.getActiveStreams()
      .then(({ data }) => setActiveStreams(data))
      .catch((err) => console.error('[LivePlayerView] failed to fetch streams', err));
    return () => {
      disconnectScreenStream();
    };
  }, []);

  const handleJoinStream = useCallback(async (sessionId: string) => {
    try {
      setStreamError(null);
      setConnectionStatus('connecting');
      setSelectedSessionId(sessionId);

      if (frameHandlerRef.current) offFrameReceived(frameHandlerRef.current);
      if (endHandlerRef.current) offStreamEnded(endHandlerRef.current);
      if (errorHandlerRef.current) offStreamError(errorHandlerRef.current);

      await disconnectScreenStream();
      const conn = await connectScreenStream();
      setConnectionStatus('connected');

      const user = (await import('../../stores/authStore')).useAuthStore.getState().user;
      const joinResult = await conn.invoke('JoinAsViewer', sessionId, user?.displayName ?? 'Admin');
      setIsViewing(true);
      setFrameCount(0);

      if (joinResult?.targetFps) {
        setFps(joinResult.targetFps);
      }

      const onFrame = (frame: IScreenFrame) => {
        setCurrentFrame(frame);
        setFrameCount((prev) => prev + 1);
        if (canvasRef.current) {
          const ctx = canvasRef.current.getContext('2d');
          if (ctx && frame.imageData) {
            const img = new Image();
            img.onload = () => {
              canvasRef.current!.width = frame.width || img.width;
              canvasRef.current!.height = frame.height || img.height;
              ctx.drawImage(img, 0, 0);
            };
            img.src = `data:image/${frame.format};base64,${frame.imageData}`;
          }
        }
      };

      const onEnd = () => {
        setIsViewing(false);
        setConnectionStatus('disconnected');
        setSelectedSessionId(null);
      };

      const onErr = (msg: string) => {
        setStreamError(msg);
      };

      frameHandlerRef.current = onFrame;
      endHandlerRef.current = onEnd;
      errorHandlerRef.current = onErr;

      onFrameReceived(onFrame);
      onStreamEnded(onEnd);
      onStreamError(onErr);

    } catch (err) {
      setStreamError(err instanceof Error ? err.message : 'Failed to join stream');
      setConnectionStatus('disconnected');
    }
  }, []);

  const handleLeaveStream = useCallback(async () => {
    if (selectedSessionId) {
      const conn = getScreenStreamConnection();
      if (conn) {
        await conn.invoke('LeaveStream', selectedSessionId).catch(() => {});
      }
    }
    await disconnectScreenStream();
    setIsViewing(false);
    setConnectionStatus('disconnected');
    setCurrentFrame(null);
    setSelectedSessionId(null);
  }, [selectedSessionId]);

  const handleCaptureScreenshot = useCallback(async () => {
    if (!targetPlayer) return;
    try {
      await screenCaptureApi.requestScreenshotFromPlayer(targetPlayer, 'manual: admin request');
    } catch (err) {
      console.error('[LivePlayerView] screenshot request failed', err);
    }
  }, [targetPlayer]);

  const handleSearchPlayer = async (query: string) => {
    if (!query.trim()) {
      setPlayerSearchResults([]);
      setShowSearch(false);
      return;
    }
    setSearching(true);
    setShowSearch(true);
    try {
      const { data } = await moderatorApi.searchPlayers({ q: query });
      setPlayerSearchResults(data.slice(0, 5));
    } catch {
      setPlayerSearchResults([]);
    } finally {
      setSearching(false);
    }
  };

  const handleSelectPlayer = (player: IPlayerSearchResult) => {
    setTargetPlayer(player.id);
    setShowSearch(false);
    setPlayerSearchResults([]);
  };

  const handleRequestFpsUpdate = useCallback(async (newFps: number) => {
    if (!selectedSessionId) return;
    setFps(newFps);
    const conn = getScreenStreamConnection();
    if (conn) {
      await conn.invoke('RequestFpsUpdate', selectedSessionId, newFps).catch(() => {});
    }
  }, [selectedSessionId]);

  return (
    <div className="space-y-6" onClick={() => setShowSearch(false)}>
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Live Player View</h1>
          <p className="text-sm text-white/30 mt-0.5">Real-time screen surveillance & evidence capture</p>
        </div>
        <div className="flex items-center gap-3">
          {connectionStatus === 'connected' && (
            <span className="flex items-center gap-1.5 text-[10px] text-emerald-400">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
              Live
            </span>
          )}
          <AnimatedButton variant="gradient" icon={<Camera size={14} />} onClick={handleCaptureScreenshot}>
            Capture Screenshot
          </AnimatedButton>
        </div>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <GlassCard className="p-4 col-span-1">
          <div className="flex items-center gap-2 mb-3">
            <Video size={14} className="text-primary-400" />
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Active Streams</h3>
          </div>

          <div className="flex gap-2 mb-3" onClick={(e) => e.stopPropagation()}>
            <div className="relative flex-1">
              <input
                type="text"
                placeholder="Search by username or ID..."
                value={targetPlayer}
                onChange={(e) => {
                  setTargetPlayer(e.target.value);
                  handleSearchPlayer(e.target.value);
                }}
                onFocus={() => targetPlayer && setShowSearch(true)}
                className="w-full bg-white/5 border border-white/10 rounded px-2 py-1 text-xs text-white placeholder-white/20 outline-none focus:border-primary-500/50"
              />
              {showSearch && playerSearchResults.length > 0 && (
                <div className="absolute top-full left-0 right-0 mt-1 bg-[#1a1f2e] border border-white/10 rounded-lg shadow-lg z-10 max-h-48 overflow-y-auto">
                  {playerSearchResults.map((p) => (
                    <button
                      key={p.id}
                      onClick={() => handleSelectPlayer(p)}
                      className="w-full flex items-center justify-between px-3 py-2 text-left hover:bg-white/5 transition-colors"
                    >
                      <div className="flex items-center gap-2">
                        <div className="w-6 h-6 rounded bg-primary-500/20 flex items-center justify-center text-[10px] font-bold text-primary-300">
                          {p.username.charAt(0)}
                        </div>
                        <span className="text-xs text-white/70">{p.username}</span>
                      </div>
                      <span className="text-[10px] text-white/30">{p.status}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
            <AnimatedButton variant="ghost" size="sm" icon={<Plus size={12} />}
              onClick={async () => {
                if (!targetPlayer) return;
                try {
                  const { data } = await screenCaptureApi.startStreamOnPlayer(targetPlayer);
                  setActiveStreams((prev) => [...prev, {
                    sessionId: data.sessionId,
                    playerId: data.playerId,
                    status: 'active',
                    startedAt: new Date().toISOString(),
                    durationSeconds: 0,
                    totalFrames: 0,
                    viewerCount: 0,
                    linkedDetectionId: undefined,
                  }]);
                } catch (err) {
                  console.error('[LivePlayerView] failed to start stream', err);
                }
              }}
            >New</AnimatedButton>
          </div>

          <div className="space-y-1 max-h-64 overflow-y-auto">
            {activeStreams.filter((s) => s.status === 'active').map((stream) => (
              <button
                key={stream.sessionId}
                onClick={() => handleJoinStream(stream.sessionId)}
                className={`w-full flex items-center justify-between p-2 rounded text-left text-xs transition-all ${
                  selectedSessionId === stream.sessionId
                    ? 'bg-primary-500/20 text-primary-300'
                    : 'text-white/50 hover:bg-white/5 hover:text-white/70'
                }`}
              >
                <div className="flex items-center gap-2">
                  <Activity size={12} className="text-emerald-400" />
                  <span>{stream.playerId}</span>
                </div>
                <span className="text-[10px] text-white/30">{stream.viewerCount} viewers</span>
              </button>
            ))}
            {activeStreams.filter((s) => s.status === 'active').length === 0 && (
              <p className="text-[10px] text-white/20 text-center py-4">No active streams</p>
            )}
          </div>
        </GlassCard>

        <div className="col-span-2 space-y-4">
          <GlassCard className={`p-4 ${isViewing ? '' : 'flex items-center justify-center min-h-[300px]'}`}>
            {isViewing && selectedSessionId ? (
              <div className="space-y-2">
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-white/40">Stream: {selectedSessionId.slice(0, 8)}...</span>
                    <span className="text-[10px] text-white/30 font-mono">{frameCount} frames</span>
                    <span className="text-[10px] text-white/30 font-mono">{fps} FPS</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => handleRequestFpsUpdate(1)}
                      className={`px-2 py-0.5 rounded text-[10px] ${fps === 1 ? 'bg-primary-500/30 text-primary-300' : 'bg-white/5 text-white/40 hover:text-white/60'}`}
                    >1x</button>
                    <button
                      onClick={() => handleRequestFpsUpdate(7)}
                      className={`px-2 py-0.5 rounded text-[10px] ${fps === 7 ? 'bg-amber-500/30 text-amber-300' : 'bg-white/5 text-white/40 hover:text-white/60'}`}
                    >7x</button>
                    <button
                      onClick={() => handleRequestFpsUpdate(15)}
                      className={`px-2 py-0.5 rounded text-[10px] ${fps === 15 ? 'bg-rose-500/30 text-rose-300' : 'bg-white/5 text-white/40 hover:text-white/60'}`}
                    >15x</button>
                    <button
                      onClick={handleLeaveStream}
                      className="flex items-center gap-1 px-2 py-0.5 rounded text-[10px] bg-rose-500/20 text-rose-400 hover:bg-rose-500/30"
                    >
                      <X size={10} /> Leave
                    </button>
                  </div>
                </div>
                <div className="relative bg-black/40 rounded-lg overflow-hidden" style={{ minHeight: 280 }}>
                  <canvas ref={canvasRef} className="w-full h-full object-contain" />
                  {streamError && (
                    <div className="absolute top-2 right-2 bg-rose-500/20 border border-rose-500/30 rounded px-2 py-1 text-[10px] text-rose-400">
                      {streamError}
                    </div>
                  )}
                </div>
              </div>
            ) : (
              <div className="text-center text-white/20">
                <Monitor size={32} className="mx-auto mb-2 opacity-50" />
                <p className="text-xs">Select an active stream or create one</p>
              </div>
            )}
          </GlassCard>

          <div className="grid grid-cols-2 gap-4">
            <GlassCard className="p-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-2">Manual Screenshot</h3>
              <div className="flex gap-2" onClick={(e) => e.stopPropagation()}>
                <div className="relative flex-1">
                  <input
                    type="text"
                    placeholder="Search by username or ID..."
                    value={targetPlayer}
                    onChange={(e) => {
                      setTargetPlayer(e.target.value);
                      handleSearchPlayer(e.target.value);
                    }}
                    onFocus={() => targetPlayer && setShowSearch(true)}
                    className="w-full bg-white/5 border border-white/10 rounded px-2 py-1 text-xs text-white placeholder-white/20 outline-none focus:border-primary-500/50"
                  />
                  {showSearch && playerSearchResults.length > 0 && (
                    <div className="absolute top-full left-0 right-0 mt-1 bg-[#1a1f2e] border border-white/10 rounded-lg shadow-lg z-10 max-h-48 overflow-y-auto">
                      {playerSearchResults.map((p) => (
                        <button
                          key={p.id}
                          onClick={() => handleSelectPlayer(p)}
                          className="w-full flex items-center justify-between px-3 py-2 text-left hover:bg-white/5 transition-colors"
                        >
                          <div className="flex items-center gap-2">
                            <div className="w-6 h-6 rounded bg-primary-500/20 flex items-center justify-center text-[10px] font-bold text-primary-300">
                              {p.username.charAt(0)}
                            </div>
                            <span className="text-xs text-white/70">{p.username}</span>
                          </div>
                          <span className="text-[10px] text-white/30">{p.status}</span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
                <AnimatedButton variant="gradient" size="sm" icon={<Camera size={12} />} onClick={handleCaptureScreenshot}>
                  Capture
                </AnimatedButton>
              </div>
            </GlassCard>

            <GlassCard className="p-4">
              <div className="flex items-center justify-between mb-2">
                <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Screenshots</h3>
                <button
                  onClick={async () => {
                    if (!targetPlayer) return;
                    const { data } = await screenCaptureApi.getScreenshotHistory(targetPlayer);
                    setScreenshotHistory(data);
                  }}
                  className="text-[10px] text-primary-400 hover:text-primary-300"
                >
                  Refresh
                </button>
              </div>
              <div className="space-y-1 max-h-48 overflow-y-auto">
                {screenshotHistory.map((s) => (
                  <div key={s.id} className="flex items-center justify-between p-1.5 rounded bg-white/[0.02]">
                    <div className="flex items-center gap-2">
                      <Camera size={10} className="text-white/30" />
                      <span className="text-[10px] text-white/40">{s.capturedAt.slice(11, 19)}</span>
                      {s.detectionEventId && (
                        <span className="text-[8px] text-amber-400/60 bg-amber-500/10 px-1 rounded">linked</span>
                      )}
                    </div>
                    <div className="flex items-center gap-1">
                      {s.storagePath && (
                        <a
                          href={`${import.meta.env.VITE_API_BASE_URL || 'http://25.20.173.193:5000'}/${s.storagePath}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-[10px] text-primary-400 hover:text-primary-300"
                        >
                          <Download size={10} />
                        </a>
                      )}
                    </div>
                  </div>
                ))}
                {screenshotHistory.length === 0 && (
                  <p className="text-[10px] text-white/20 text-center py-2">No screenshots captured</p>
                )}
              </div>
            </GlassCard>
          </div>
        </div>

        <GlassCard className="p-4 col-span-1">
          <div className="flex items-center gap-2 mb-3">
            <Users size={14} className="text-primary-400" />
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Stream History</h3>
          </div>
          <div className="space-y-1 max-h-96 overflow-y-auto">
            {activeStreams.map((stream) => (
              <div key={stream.sessionId} className="p-2 rounded bg-white/[0.02]">
                <div className="flex items-center justify-between">
                  <span className="text-[10px] font-mono text-white/50">{stream.playerId}</span>
                  <span className={`text-[8px] px-1.5 py-0.5 rounded-full ${
                    stream.status === 'active' ? 'bg-emerald-500/20 text-emerald-400' :
                    stream.status === 'ended' ? 'bg-white/10 text-white/30' :
                    'bg-amber-500/20 text-amber-400'
                  }`}>
                    {stream.status}
                  </span>
                </div>
                <div className="flex items-center gap-3 mt-1 text-[9px] text-white/20">
                  <span className="flex items-center gap-1"><Activity size={8} />{stream.totalFrames}f</span>
                  <span className="flex items-center gap-1"><Clock size={8} />{stream.durationSeconds.toFixed(0)}s</span>
                  <span className="flex items-center gap-1"><Users size={8} />{stream.viewerCount}</span>
                </div>
              </div>
            ))}
            {activeStreams.length === 0 && (
              <p className="text-[10px] text-white/20 text-center py-4">No stream history</p>
            )}
          </div>
        </GlassCard>
      </div>
    </div>
  );
}
