import { useEffect, useState, useCallback } from 'react';
import { motion } from 'framer-motion';
import {
  Plus, ToggleLeft, ToggleRight, Edit2, Trash2, AlertTriangle,
  Shield, Activity, Hash, Tag, Code,
} from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { AnimatedModal } from '../../components/ui/AnimatedModal';
import { Select } from '../../components/ui/Select';
import { DataTable } from '../../components/ui/DataTable';
import { superAdminApi } from '../../services/superadmin';
import { useUIStore } from '../../stores/uiStore';
import type { IRuleEntry } from '../../services/superadmin';

const MATCH_TYPES = [
  'injection_api_set', 'dangerous_import', 'lua_dll', 'packed_unsigned',
  'high_entropy', 'high_entropy_overlay', 'suspicious_pdb', 'self_signed_game_file',
  'suspicious_section_name', 'rwx_section', 'low_entropy_code', 'rsrc_executable',
  'tls_callbacks', 'unsigned_dll_game_dir', 'process_name', 'file_path',
];

const DEFAULT_FORM = {
  name: '', description: '', severity: 'medium', category: '', matchType: 'process_name',
  patterns: '', tags: '', conditionsJson: '',
};

export function RulesPage() {
  const [rules, setRules] = useState<IRuleEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [form, setForm] = useState(DEFAULT_FORM);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const { addToast } = useUIStore();

  const fetchRules = useCallback(() => {
    superAdminApi.getRules()
      .then(({ data }) => setRules(data))
      .catch((err) => {
        console.error('RulesPage', err);
        addToast({ type: 'error', title: 'Error', message: 'Failed to load rules' });
      })
      .finally(() => setLoading(false));
  }, [addToast]);

  useEffect(() => { fetchRules(); }, [fetchRules]);

  const openCreate = () => {
    setEditId(null);
    setForm(DEFAULT_FORM);
    setModalOpen(true);
  };

  const openEdit = (rule: IRuleEntry) => {
    setEditId(rule.id);
    setForm({
      name: rule.name,
      description: rule.description,
      severity: rule.severity,
      category: rule.category,
      matchType: rule.matchType,
      patterns: (rule.patterns || []).join(', '),
      tags: (rule.tags || []).join(', '),
      conditionsJson: rule.conditions ? JSON.stringify(rule.conditions, null, 2) : '',
    });
    setModalOpen(true);
  };

  const handleSave = async () => {
    if (!form.name.trim()) {
      addToast({ type: 'error', title: 'Validation', message: 'Rule name is required' });
      return;
    }

    const parseList = (s: string) => s.split(',').map(x => x.trim()).filter(Boolean);

    let conditions = null;
    if (form.conditionsJson.trim()) {
      try { conditions = JSON.parse(form.conditionsJson); }
      catch { addToast({ type: 'error', title: 'Invalid JSON', message: 'Conditions must be valid JSON' }); return; }
    }

    const payload = {
      name: form.name.trim(),
      description: form.description.trim(),
      severity: form.severity,
      category: form.category.trim(),
      matchType: form.matchType,
      patterns: parseList(form.patterns),
      tags: parseList(form.tags),
      conditions,
      enabled: true,
    };

    try {
      if (editId) {
        await superAdminApi.updateRule(editId, payload);
        addToast({ type: 'success', title: 'Updated', message: `"${form.name}" updated` });
      } else {
        await superAdminApi.createRule(payload);
        addToast({ type: 'success', title: 'Created', message: `"${form.name}" created` });
      }
      setModalOpen(false);
      fetchRules();
    } catch {
      addToast({ type: 'error', title: 'Error', message: editId ? 'Failed to update rule' : 'Failed to create rule' });
    }
  };

  const handleToggle = async (rule: IRuleEntry) => {
    try {
      await superAdminApi.toggleRule(rule.id);
      addToast({ type: 'success', title: rule.enabled ? 'Disabled' : 'Enabled', message: `"${rule.name}" ${rule.enabled ? 'disabled' : 'enabled'}` });
      fetchRules();
    } catch {
      addToast({ type: 'error', title: 'Error', message: 'Failed to toggle rule' });
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await superAdminApi.deleteRule(id);
      addToast({ type: 'success', title: 'Deleted', message: 'Rule deleted' });
      setConfirmDelete(null);
      fetchRules();
    } catch {
      addToast({ type: 'error', title: 'Error', message: 'Failed to delete rule' });
    }
  };

  const enabledCount = rules.filter(r => r.enabled).length;
  const totalHits = rules.reduce((s, r) => s + (r.hitCount || 0), 0);

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Engine Rules</h1>
          <p className="text-sm text-white/30 mt-0.5">Manage detection rules — changes apply immediately without restart</p>
        </div>
        <AnimatedButton variant="gradient" icon={<Plus size={14} />} onClick={openCreate}>New Rule</AnimatedButton>
      </motion.div>

      <div className="grid grid-cols-3 gap-4">
        <GlassCard className="p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-primary-500/10 flex items-center justify-center"><Shield size={18} className="text-primary-400" /></div>
          <div><p className="text-2xl font-bold text-white">{rules.length}</p><p className="text-[10px] text-white/30 uppercase tracking-wider">Total Rules</p></div>
        </GlassCard>
        <GlassCard className="p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-emerald-500/10 flex items-center justify-center"><Activity size={18} className="text-emerald-400" /></div>
          <div><p className="text-2xl font-bold text-white">{enabledCount}</p><p className="text-[10px] text-white/30 uppercase tracking-wider">Active Rules</p></div>
        </GlassCard>
        <GlassCard className="p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-amber-500/10 flex items-center justify-center"><AlertTriangle size={18} className="text-amber-400" /></div>
          <div><p className="text-2xl font-bold text-white">{totalHits}</p><p className="text-[10px] text-white/30 uppercase tracking-wider">Total Hits</p></div>
        </GlassCard>
      </div>

      <GlassCard className="p-6">
        <DataTable
            columns={[
            { key: 'name', label: 'Rule Name', sortable: true },
            { key: 'category', label: 'Category', sortable: true },
            { key: 'matchType', label: 'Match Type', sortable: true },
            {
              key: 'severity', label: 'Severity', sortable: true,
              render: (item: Record<string, unknown>) => (
                <span className={`text-[10px] px-2 py-0.5 rounded-full ${
                  String(item.severity) === 'critical' ? 'bg-rose-500/20 text-rose-400' :
                  String(item.severity) === 'high' ? 'bg-amber-500/20 text-amber-400' :
                  String(item.severity) === 'medium' ? 'bg-primary-500/20 text-primary-300' :
                  'bg-white/5 text-white/30'
                }`}>{String(item.severity)}</span>
              ),
            },
            {
              key: 'enabled', label: 'Enabled', sortable: true,
              render: (item: Record<string, unknown>) => (
                <button onClick={(e) => { e.stopPropagation(); handleToggle(item as IRuleEntry); }}
                  className={`flex items-center gap-1 text-[10px] transition-colors ${item.enabled ? 'text-emerald-400 hover:text-emerald-300' : 'text-white/30 hover:text-white/50'}`}>
                  {item.enabled ? <ToggleRight size={12} /> : <ToggleLeft size={12} />}
                  {item.enabled ? 'Enabled' : 'Disabled'}
                </button>
              ),
            },
            { key: 'hitCount', label: 'Hits', sortable: true },
            {
              key: 'lastMatchTime', label: 'Last Match',
              render: (item: Record<string, unknown>) => (
                <span className="text-[10px] text-white/30">{item.lastMatchTime ? new Date(String(item.lastMatchTime)).toLocaleDateString() : 'Never'}</span>
              ),
            },
            {
              key: 'id', label: 'Actions', width: '80px',
              render: (item: Record<string, unknown>) => (
                <div className="flex items-center gap-1">
                  <button onClick={(e) => { e.stopPropagation(); openEdit(item as IRuleEntry); }} className="p-1.5 rounded-lg hover:bg-white/5 text-white/30 hover:text-primary-400 transition-colors">
                    <Edit2 size={12} />
                  </button>
                  <button onClick={(e) => { e.stopPropagation(); setConfirmDelete(item.id as string); }} className="p-1.5 rounded-lg hover:bg-white/5 text-white/30 hover:text-rose-400 transition-colors">
                    <Trash2 size={12} />
                  </button>
                </div>
              ),
            },
          ]}
          data={rules}
          keyExtractor={(r: Record<string, unknown>) => r.id as string}
          searchable
          searchKeys={['name', 'category', 'matchType', 'description']}
          onRowClick={(item: Record<string, unknown>) => openEdit(item as IRuleEntry)}
        />
      </GlassCard>

      <AnimatedModal open={modalOpen} onClose={() => setModalOpen(false)}
        title={editId ? 'Edit Rule' : 'New Rule'} width="max-w-2xl">
        <div className="space-y-4 max-h-[60vh] overflow-y-auto pr-2">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Rule Name *</label>
              <input value={form.name} onChange={(e) => setForm(f => ({ ...f, name: e.target.value }))}
                placeholder="e.g. Detect-Injection-API"
                className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Category</label>
              <input value={form.category} onChange={(e) => setForm(f => ({ ...f, category: e.target.value }))}
                placeholder="e.g. injection, memory, process"
                className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
          </div>

          <div>
            <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Description</label>
            <textarea value={form.description} onChange={(e) => setForm(f => ({ ...f, description: e.target.value }))}
              placeholder="Describe what this rule detects..."
              className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30 resize-none h-16" />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Severity</label>
              <Select value={form.severity} onChange={(e) => setForm(f => ({ ...f, severity: e.target.value }))}>
                <option value="low">Low</option>
                <option value="medium">Medium</option>
                <option value="high">High</option>
                <option value="critical">Critical</option>
              </Select>
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">Match Type *</label>
              <Select value={form.matchType} onChange={(e) => setForm(f => ({ ...f, matchType: e.target.value }))}>
                {MATCH_TYPES.map(mt => <option key={mt} value={mt}>{mt}</option>)}
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">
                <Tag size={10} className="inline mr-1" />Patterns (comma-separated)
              </label>
              <input value={form.patterns} onChange={(e) => setForm(f => ({ ...f, patterns: e.target.value }))}
                placeholder="cheat_engine.exe, *.inject*"
                className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">
                <Hash size={10} className="inline mr-1" />Tags (comma-separated)
              </label>
              <input value={form.tags} onChange={(e) => setForm(f => ({ ...f, tags: e.target.value }))}
                placeholder="injection, memory, critical"
                className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30" />
            </div>
          </div>

          <div>
            <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1.5">
              <Code size={10} className="inline mr-1" />Conditions (JSON)
            </label>
            <textarea value={form.conditionsJson} onChange={(e) => setForm(f => ({ ...f, conditionsJson: e.target.value }))}
              placeholder={`{\n  "minApiCount": 3,\n  "apis": ["OpenProcess", "WriteProcessMemory"],\n  "entropyThreshold": 7.0\n}`}
              className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30 resize-none font-mono h-24" />
            <p className="text-[9px] text-white/20 mt-1">Optional. Structure depends on matchType. See docs for schema.</p>
          </div>

          <div className="flex gap-2 pt-2 border-t border-white/5">
            <AnimatedButton variant="secondary" onClick={() => setModalOpen(false)} fullWidth>Cancel</AnimatedButton>
            <AnimatedButton variant="gradient" onClick={handleSave} fullWidth>{editId ? 'Save Changes' : 'Create Rule'}</AnimatedButton>
          </div>
        </div>
      </AnimatedModal>

      <AnimatedModal open={confirmDelete !== null} onClose={() => setConfirmDelete(null)} title="Delete Rule">
        <div className="space-y-4">
          <div className="flex items-center gap-3 p-3 rounded-xl bg-rose-500/5 border border-rose-500/10">
            <AlertTriangle size={16} className="text-rose-400 shrink-0" />
            <p className="text-xs text-white/50">This will permanently remove this rule from the engine. Active detections referencing this rule will stop.</p>
          </div>
          <div className="flex gap-2">
            <AnimatedButton variant="secondary" onClick={() => setConfirmDelete(null)} fullWidth>Cancel</AnimatedButton>
            <AnimatedButton variant="danger" onClick={() => confirmDelete && handleDelete(confirmDelete)} fullWidth>Delete</AnimatedButton>
          </div>
        </div>
      </AnimatedModal>
    </div>
  );
}
