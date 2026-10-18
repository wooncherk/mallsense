import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Bold, Building2, Check, ChevronDown, Edit3, EllipsisVertical, File, Globe2, Heading1, Heading2, ImagePlus, Italic, Link, List, ListOrdered, Map, MessageSquarePlus, Paperclip, Plus, Quote, Search, Send, SlidersHorizontal, Sparkles, Trash2, X } from 'lucide-react';
import {
  acceptKnowledgeSuggestion, createKnowledgeSuggestionWithProgress,
  deleteKnowledgeSource, generateFloorPlanDraft, getAllStores, getKnowledgeCollections,
  getKnowledgeSources,
  importKnowledgeText, publishFloorPlanDraft,
  rejectKnowledgeSuggestion, updateFloorPlanDraft, updateKnowledgeSource, updateKnowledgeStatus,
} from '../api';
import type { FloorPlanDraftDto, KnowledgeAiProgressDto, KnowledgeCollectionDto, KnowledgeSourceDto, KnowledgeSuggestionDto, StoreDto } from '../types';
type Panel = 'create' | 'edit' | null;

export function KnowledgeWorkspace({ isAdmin }: { isAdmin: boolean }) {
  const [collections, setCollections] = useState<KnowledgeCollectionDto[]>([]);
  const [sources, setSources] = useState<KnowledgeSourceDto[]>([]);
  const [stores, setStores] = useState<StoreDto[]>([]);
  const [ownerFilter, setOwnerFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [kindFilter, setKindFilter] = useState('all');
  const [query, setQuery] = useState('');
  const [panel, setPanel] = useState<Panel>(null);
  const [selected, setSelected] = useState<KnowledgeSourceDto | null>(null);
  const [collectionId, setCollectionId] = useState('');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [sourceKind, setSourceKind] = useState<'General' | 'Deal' | 'Event'>('General');
  const [newStatus, setNewStatus] = useState<'Draft' | 'Published'>('Draft');
  const [files, setFiles] = useState<File[]>([]);
  const [floorNumber, setFloorNumber] = useState(1);
  const [floorName, setFloorName] = useState('Level 1');
  const [floorDraft, setFloorDraft] = useState<FloorPlanDraftDto | null>(null);
  const [graphJson, setGraphJson] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const reload = async (preferredId?: string) => {
    const [scopeData, sourceData, storeData] = await Promise.all([getKnowledgeCollections(), getKnowledgeSources(), getAllStores()]);
    setCollections(scopeData); setSources(sourceData); setStores(storeData);
    const writable = scopeData.find((x) => x.allowedActions.includes('knowledge.import')) || scopeData[0];
    setCollectionId((current) => current || writable?.id || '');
    if (preferredId) setSelected(sourceData.find((x) => x.id === preferredId) || null);
  };
  useEffect(() => { reload().catch((e: Error) => setError(e.message)); }, []);

  const run = async (operation: () => Promise<void>) => {
    setBusy(true); setError('');
    try { await operation(); } catch (e) { setError(e instanceof Error ? e.message : 'The operation failed.'); }
    finally { setBusy(false); }
  };
  const belongsTo = (scope?: KnowledgeCollectionDto) => {
    if (!scope) return 'Unknown';
    if (scope.scopeType === 'Global') return 'Mall-wide';
    if (scope.scopeType === 'Tenant') return stores.find((x) => x.id === scope.scopeReferenceId)?.name || `Tenant ${scope.scopeReferenceId?.slice(0, 8)}`;
    return scope.scopeType;
  };
  const visibleSources = useMemo(() => {
    const value = query.trim().toLowerCase();
    return sources.filter((source) => (ownerFilter === 'all' || source.collectionId === ownerFilter) &&
      (statusFilter === 'all' || source.status === statusFilter) &&
      (kindFilter === 'all' || source.sourceKind === kindFilter) &&
      (!value || `${source.title} ${source.collectionName} ${source.sourceKind} ${source.aiSummary} ${source.content}`.toLowerCase().includes(value)));
  }, [kindFilter, ownerFilter, query, sources, statusFilter]);

  const openCreate = () => {
    const writableScopes = collections.filter((x) => x.allowedActions.includes('knowledge.import'));
    const writable = collections.find((x) => ownerFilter !== 'all' && x.id === ownerFilter && x.allowedActions.includes('knowledge.import')) ||
      writableScopes.find((x) => x.scopeType === 'Global') || writableScopes[0];
    setCollectionId(writable?.id || ''); setTitle(''); setContent(''); setSourceKind('General'); setNewStatus('Draft'); setFiles([]); setSelected(null); setPanel('create');
  };
  const openEdit = (source: KnowledgeSourceDto) => {
    setSelected(source); setCollectionId(source.collectionId); setTitle(source.title);
    setSourceKind(source.sourceKind === 'Deal' || source.sourceKind === 'Event' ? source.sourceKind : 'General');
    const originalContent = source.content === '[binary source retained]' ? '' : removeDuplicateTitleHeading(source.content, source.title);
    const documentContent = promoteDescriptiveMetadataToBody(originalContent);
    setContent(documentContent);
    setFiles([]); setFloorDraft(null); setPanel('edit');
    // Upgrade older documents when they are opened so human-facing details no longer remain hidden in YAML.
    if (documentContent !== originalContent) {
      void run(async () => {
        const updated = await updateKnowledgeSource(source.id, {
          title: source.title, collectionId: source.collectionId,
          sourceKind: source.sourceKind === 'Deal' || source.sourceKind === 'Event' ? source.sourceKind : 'General',
          content: documentContent,
        });
        setSelected(updated);
        setSources((current) => current.map((item) => item.id === updated.id ? updated : item));
      });
    }
  };
  const createKnowledge = () => run(async () => {
    if (!collectionId) throw new Error('Choose who this knowledge belongs to.');
    if (!title.trim()) throw new Error('Enter a document title.');
    let created = [await importKnowledgeText({ collectionId, title, content, sourceKind, mimeType: 'text/markdown' })];
    if (newStatus === 'Published') created = await Promise.all(created.map((source) => updateKnowledgeStatus(source.id, 'Published')));
    await reload(created.at(-1)?.id); setPanel(null);
  });
  const createKnowledgeWithAi = (instruction: string, evidence: File[], onProgress: (progress: KnowledgeAiProgressDto) => void) => run(async () => {
    if (!collectionId) throw new Error('Choose who this knowledge belongs to.');
    const draft = await importKnowledgeText({ collectionId, title: title.trim() || 'Untitled document', content, sourceKind, mimeType: 'text/markdown' });
    const updated = await createKnowledgeSuggestionWithProgress(draft.id, instruction, evidence, onProgress);
    setSelected(updated); setTitle(updated.title); setContent(promoteDescriptiveMetadataToBody(removeDuplicateTitleHeading(updated.content, updated.title)));
    setFiles([]); setSources((current) => [updated, ...current.filter((source) => source.id !== updated.id)]); setPanel('edit');
  });
  const updateSource = () => run(async () => {
    if (!selected) return;
    const updated = await updateKnowledgeSource(selected.id, { title, collectionId, sourceKind, content: selected.content === '[binary source retained]' ? undefined : content });
    setSelected(updated); await reload(updated.id);
  });
  const changeStatus = (status: 'Draft' | 'Published') => run(async () => {
    if (!selected) return;
    const saved = await updateKnowledgeSource(selected.id, { title, collectionId, sourceKind, content: selected.content === '[binary source retained]' ? undefined : content });
    const updated = await updateKnowledgeStatus(saved.id, status);
    setSelected(updated); setSources((current) => current.map((source) => source.id === updated.id ? updated : source));
  });
  const requestAiSuggestion = (instruction: string, evidence: File[], onProgress: (progress: KnowledgeAiProgressDto) => void) => run(async () => {
    if (!selected) return;
    await updateKnowledgeSource(selected.id, { title, collectionId, sourceKind, content: selected.content === '[binary source retained]' ? undefined : content });
    const updated = await createKnowledgeSuggestionWithProgress(selected.id, instruction, evidence, onProgress);
    setSelected(updated); setSources((current) => current.map((source) => source.id === updated.id ? updated : source));
  });
  const acceptAiSuggestion = (suggestionId: string) => run(async () => {
    if (!selected) return;
    const updated = await acceptKnowledgeSuggestion(selected.id, suggestionId, 'Suggest');
    setSelected(updated); setContent(promoteDescriptiveMetadataToBody(removeDuplicateTitleHeading(updated.content, updated.title)));
    setSources((current) => current.map((source) => source.id === updated.id ? updated : source));
  });
  const rejectAiSuggestion = (suggestionId: string) => run(async () => {
    if (!selected) return;
    const updated = await rejectKnowledgeSuggestion(selected.id, suggestionId);
    setSelected(updated); setSources((current) => current.map((source) => source.id === updated.id ? updated : source));
  });
  const removeSource = (source: KnowledgeSourceDto) => {
    if (!window.confirm(`Delete “${source.title}”? This also removes its attachments and unpublished floor-plan drafts.`)) return;
    run(async () => { await deleteKnowledgeSource(source.id); if (selected?.id === source.id) { setSelected(null); setPanel(null); } await reload(); });
  };
  const makeFloorPlan = () => run(async () => {
    if (!selected) return;
    const draft = await generateFloorPlanDraft(selected.id, floorNumber, floorName);
    setFloorDraft(draft); setGraphJson(JSON.stringify({ nodes: draft.nodes, edges: draft.edges, warnings: draft.warnings }, null, 2));
  });
  return <div className="mx-auto max-w-7xl space-y-5">
    <section className="admin-page-header flex flex-wrap items-end justify-between gap-5 p-6 md:p-8"><div><h2 className="text-3xl font-bold tracking-[-0.04em]">Knowledge</h2><p className="mt-2 text-sm text-base-content/55">{isAdmin ? 'Manage mall-wide and tenant knowledge.' : 'Manage your tenant knowledge.'}</p></div><button className="btn btn-primary" onClick={openCreate}><Plus size={16} /> Create or upload</button></section>
    {error && <div className="alert alert-error py-3 text-sm">{error}</div>}
    <section className="surface-card p-5"><div className="flex flex-col gap-3 md:flex-row"><label className="input input-bordered flex flex-1 items-center gap-2"><Search size={16} className="text-base-content/40" /><input className="grow" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search knowledge" /></label><KnowledgeFilterMenu collections={collections} ownerFilter={ownerFilter} setOwnerFilter={setOwnerFilter} statusFilter={statusFilter} setStatusFilter={setStatusFilter} kindFilter={kindFilter} setKindFilter={setKindFilter} belongsTo={belongsTo} showOwner={isAdmin} /></div></section>
    <section className="overflow-hidden rounded-2xl border border-base-300 bg-base-100"><div className="flex items-center justify-between border-b border-base-300 px-5 py-4"><h3 className="font-bold">Knowledge</h3><span className="text-xs text-base-content/45">{visibleSources.length} items</span></div><div className="overflow-x-auto"><table className="table"><thead><tr><th>Name</th>{isAdmin && <th>Belongs to</th>}<th>Status</th><th>Added</th><th className="text-right">Actions</th></tr></thead><tbody>
      {visibleSources.map((source) => { const scope = collections.find((x) => x.id === source.collectionId); const canEdit = scope?.allowedActions.includes('knowledge.edit'); return <tr key={source.id} className="hover:bg-base-200/50"><td><button className="flex min-w-48 items-center gap-2 text-left font-semibold hover:text-primary" onClick={() => openEdit(source)}><File size={15} className="text-primary" />{source.title}</button><div className="mt-1 max-w-xs truncate text-xs text-base-content/40">{source.aiSummary}</div></td>{isAdmin && <td className="text-sm"><Building2 className="mr-2 inline" size={14} />{belongsTo(scope)}</td>}<td><span className={`badge badge-sm ${source.status === 'Published' ? 'badge-success badge-soft' : 'badge-ghost'}`}>{source.status}</span></td><td className="whitespace-nowrap text-xs text-base-content/50">{new Date(source.createdAtUtc).toLocaleDateString()}</td><td><div className="flex justify-end gap-1"><button className="btn btn-ghost btn-xs" onClick={() => openEdit(source)}><Edit3 size={13} /> Edit</button>{canEdit && <div className="dropdown dropdown-end"><button type="button" tabIndex={0} className="btn btn-ghost btn-xs btn-square" aria-label="More options"><EllipsisVertical size={15} /></button><ul tabIndex={0} className="menu dropdown-content z-20 mt-1 w-40 rounded-box border border-base-300 bg-base-100 p-1 shadow-lg"><li><button className="text-error" onClick={() => removeSource(source)}><Trash2 size={13} /> Delete</button></li></ul></div>}</div></td></tr>; })}
      {!visibleSources.length && <tr><td colSpan={isAdmin ? 5 : 4} className="py-14 text-center text-base-content/40">No knowledge matches this view.</td></tr>}
    </tbody></table></div></section>
    {panel && <div className="modal modal-open"><div className="modal-box flex h-[94vh] w-[96vw] max-w-[96rem] flex-col overflow-hidden"><button className="btn btn-circle btn-ghost btn-sm absolute right-4 top-4 z-40" onClick={() => setPanel(null)}><X size={16} /></button>
      {panel === 'create' && <CreatePanel collections={collections} collectionId={collectionId} setCollectionId={setCollectionId} belongsTo={belongsTo} status={newStatus} setStatus={setNewStatus} sourceKind={sourceKind} setSourceKind={setSourceKind} title={title} setTitle={setTitle} content={content} setContent={setContent} files={files} setFiles={setFiles} busy={busy} submit={createKnowledge} createWithAi={createKnowledgeWithAi} />}
      {panel === 'edit' && selected && <EditPanel selected={selected} collections={collections} collectionId={collectionId} setCollectionId={setCollectionId} belongsTo={belongsTo} status={selected.status === 'Published' ? 'Published' : 'Draft'} setStatus={changeStatus} sourceKind={sourceKind} setSourceKind={setSourceKind} title={title} setTitle={setTitle} content={content} setContent={setContent} busy={busy} save={updateSource} requestAi={requestAiSuggestion} acceptAi={acceptAiSuggestion} rejectAi={rejectAiSuggestion} floorNumber={floorNumber} setFloorNumber={setFloorNumber} floorName={floorName} setFloorName={setFloorName} makeFloorPlan={makeFloorPlan} floorDraft={floorDraft} graphJson={graphJson} setGraphJson={setGraphJson} run={run} setFloorDraft={setFloorDraft} />}
    </div><button className="modal-backdrop" onClick={() => setPanel(null)}>close</button></div>}
  </div>;
}

function KnowledgeFilterMenu({ collections, ownerFilter, setOwnerFilter, statusFilter, setStatusFilter, kindFilter, setKindFilter, belongsTo, showOwner }: {
  collections: KnowledgeCollectionDto[];
  ownerFilter: string;
  setOwnerFilter: (value: string) => void;
  statusFilter: string;
  setStatusFilter: (value: string) => void;
  kindFilter: string;
  setKindFilter: (value: string) => void;
  belongsTo: (scope?: KnowledgeCollectionDto) => string;
  showOwner: boolean;
}) {
  const activeCount = [showOwner ? ownerFilter : 'all', statusFilter, kindFilter].filter((value) => value !== 'all').length;
  return <details className="dropdown dropdown-end md:self-start">
    <summary className={`btn list-none border-base-300 ${activeCount ? 'btn-primary' : 'btn-ghost bg-base-100'}`}><SlidersHorizontal size={16} /> Filters{activeCount > 0 && <span className="badge badge-sm">{activeCount}</span>}</summary>
    <div className="dropdown-content z-40 mt-2 w-[min(92vw,42rem)] rounded-2xl border border-base-300 bg-base-100 p-5 shadow-2xl">
      <div className="flex items-center justify-between border-b border-base-300 pb-3"><div><div className="font-bold">Filter knowledge</div><div className="mt-1 text-xs text-base-content/45">Narrow the table by owner, status, or content type.</div></div><button className="btn btn-ghost btn-xs" disabled={!activeCount} onClick={() => { setOwnerFilter('all'); setStatusFilter('all'); setKindFilter('all'); }}>Clear all</button></div>
      <div className={`mt-4 grid gap-5 ${showOwner ? 'md:grid-cols-3' : 'md:grid-cols-2'}`}>
        {showOwner && <label className="grid content-start gap-2 text-sm"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Owner</span><select className="select select-bordered w-full" value={ownerFilter} onChange={(event) => setOwnerFilter(event.target.value)}><option value="all">All owners</option>{collections.filter((scope) => scope.scopeType === 'Global').map((scope) => <option key={scope.id} value={scope.id}>{belongsTo(scope)}</option>)}{collections.filter((scope) => scope.scopeType === 'Tenant').sort((a, b) => belongsTo(a).localeCompare(belongsTo(b))).map((scope) => <option key={scope.id} value={scope.id}>{belongsTo(scope)}</option>)}</select></label>}
        <div className="grid content-start gap-2"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Status</span>{[['all', 'All statuses'], ['Draft', 'Draft'], ['Published', 'Published']].map(([value, label]) => <label key={value} className="flex cursor-pointer items-center gap-2 text-sm"><input className="radio radio-primary radio-sm" type="radio" name="knowledge-status-filter" checked={statusFilter === value} onChange={() => setStatusFilter(value)} />{label}</label>)}</div>
        <div className="grid content-start gap-2"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Content type</span>{[['all', 'All types'], ['General', 'General'], ['Deal', 'Deal'], ['Event', 'Event']].map(([value, label]) => <label key={value} className="flex cursor-pointer items-center gap-2 text-sm"><input className="radio radio-primary radio-sm" type="radio" name="knowledge-kind-filter" checked={kindFilter === value} onChange={() => setKindFilter(value)} />{label}</label>)}</div>
      </div>
    </div>
  </details>;
}

function CreatePanel(p: { collections: KnowledgeCollectionDto[]; collectionId: string; setCollectionId: (v: string) => void; belongsTo: (f?: KnowledgeCollectionDto) => string; status: 'Draft' | 'Published'; setStatus: (v: 'Draft' | 'Published') => void; sourceKind: 'General' | 'Deal' | 'Event'; setSourceKind: (v: 'General' | 'Deal' | 'Event') => void; title: string; setTitle: (v: string) => void; content: string; setContent: (v: string) => void; files: File[]; setFiles: (v: File[]) => void; busy: boolean; submit: () => void; createWithAi: (instruction: string, files: File[], onProgress: (progress: KnowledgeAiProgressDto) => void) => Promise<void> }) {
  const [showAi, setShowAi] = useState(true);
  const [aiPrompt, setAiPrompt] = useState('');
  const [aiContext, setAiContext] = useState('');
  const [aiWorking, setAiWorking] = useState(false);
  const [selectionAction, setSelectionAction] = useState<{ text: string; left: number; top: number } | null>(null);
  const scope = p.collections.find((item) => item.id === p.collectionId);
  const ownerLabel = p.belongsTo(scope);
  const writableScopes = p.collections.filter((item) => item.allowedActions.includes('knowledge.import'));
  const sendAi = async () => {
    if (!aiPrompt.trim()) return;
    setAiWorking(true);
    try {
      const instruction = aiContext ? `${aiPrompt.trim()}\n\nSelected passage:\n> ${aiContext}` : aiPrompt.trim();
      await p.createWithAi(instruction, p.files, () => undefined);
      setAiPrompt(''); setAiContext('');
    } finally { setAiWorking(false); }
  };
  const askAboutSelection = (text: string, rect: DOMRect) => setSelectionAction({ text, left: Math.min(rect.left, window.innerWidth - 190), top: Math.min(rect.bottom + 8, window.innerHeight - 50) });
  const useSelection = () => { if (!selectionAction) return; setShowAi(true); setAiContext(selectionAction.text); setAiPrompt('Improve this selected passage'); setSelectionAction(null); };
  return <div className="flex min-h-0 flex-1 flex-col"><div className="flex flex-wrap items-center justify-between gap-3 pr-10"><div className="flex min-w-0 items-center gap-2 text-sm font-semibold"><button type="button" className={`btn btn-sm btn-square rounded-lg ${showAi ? 'btn-secondary' : 'btn-ghost border border-base-300'}`} onClick={() => setShowAi((value) => !value)} title="AI writing assistant" aria-label="Toggle AI writing assistant"><Sparkles size={16} /></button><span className="font-bold">Knowledge</span><span className="text-base-content/35">›</span>{writableScopes.length > 1 ? <OwnerSelector scopes={writableScopes} value={p.collectionId} onChange={p.setCollectionId} belongsTo={p.belongsTo} /> : <span>{ownerLabel}</span>}</div><div className="flex flex-wrap items-end gap-3"><button type="button" className="btn btn-ghost btn-sm border border-base-300" disabled={p.busy || !p.title.trim()} onClick={p.submit}>Create draft</button><DocumentControls status={p.status} onStatusChange={p.setStatus} sourceKind={p.sourceKind} onSourceKindChange={p.setSourceKind} /></div></div><div className={`mt-4 grid min-h-0 flex-1 items-stretch gap-4 ${showAi ? 'lg:grid-cols-[21rem_minmax(0,1fr)]' : ''}`}>{showAi && <KnowledgeAiSidebar prompt={aiPrompt} setPrompt={setAiPrompt} context={aiContext} clearContext={() => setAiContext('')} files={p.files} setFiles={p.setFiles} busy={p.busy} working={aiWorking} send={sendAi} disabled={false} />}<div className="min-w-0 overflow-y-auto pr-1"><MarkdownEditor value={p.content} onChange={p.setContent} title={p.title} onTitleChange={p.setTitle} sourceKind={p.sourceKind} onSelectionAsk={askAboutSelection} placeholder="Describe the knowledge clearly. Include who it is for, when it is relevant, key conditions, and any facts shoppers should know — or ask AI to draft it." /></div></div>{selectionAction && <button type="button" className="fixed z-[120] flex items-center gap-2 rounded-lg border border-base-300 bg-base-100 px-3 py-2 text-xs font-semibold shadow-xl" style={{ left: selectionAction.left, top: selectionAction.top }} onMouseDown={(event) => event.preventDefault()} onClick={useSelection}><MessageSquarePlus size={14} className="text-secondary" />Ask AI about selection</button>}</div>;
}

function EditPanel(p: { selected: KnowledgeSourceDto; collections: KnowledgeCollectionDto[]; collectionId: string; setCollectionId: (v: string) => void; belongsTo: (f?: KnowledgeCollectionDto) => string; status: 'Draft' | 'Published'; setStatus: (v: 'Draft' | 'Published') => void; sourceKind: 'General' | 'Deal' | 'Event'; setSourceKind: (v: 'General' | 'Deal' | 'Event') => void; title: string; setTitle: (v: string) => void; content: string; setContent: (v: string) => void; busy: boolean; save: () => void; requestAi: (instruction: string, files: File[], onProgress: (progress: KnowledgeAiProgressDto) => void) => Promise<void>; acceptAi: (suggestionId: string) => Promise<void>; rejectAi: (suggestionId: string) => Promise<void>; floorNumber: number; setFloorNumber: (v: number) => void; floorName: string; setFloorName: (v: string) => void; makeFloorPlan: () => void; floorDraft: FloorPlanDraftDto | null; graphJson: string; setGraphJson: (v: string) => void; run: (fn: () => Promise<void>) => void; setFloorDraft: (v: FloorPlanDraftDto) => void }) {
  const isBinary = p.selected.content === '[binary source retained]';
  const [showAi, setShowAi] = useState(true);
  const [aiPrompt, setAiPrompt] = useState('');
  const [aiFiles, setAiFiles] = useState<File[]>([]);
  const [aiContext, setAiContext] = useState('');
  const [aiWorking, setAiWorking] = useState(false);
  const [reviewPreview, setReviewPreview] = useState<{ id: string; mode: 'before' | 'after' } | null>(null);
  const [selectionAction, setSelectionAction] = useState<{ text: string; left: number; top: number } | null>(null);
  const writableScopes = p.collections.filter((item) => item.allowedActions.includes('knowledge.import'));
  const owner = p.belongsTo(p.collections.find((item) => item.id === p.collectionId));
  const saveRef = useRef(p.save);
  const initialized = useRef(false);
  const dirty = useRef(false);
  const pendingSuggestions = p.selected.suggestions.filter((suggestion) => suggestion.status === 'Pending').slice(-3).reverse();
  const previewSuggestion = pendingSuggestions.find((suggestion) => suggestion.id === reviewPreview?.id);
  useEffect(() => { saveRef.current = p.save; }, [p.save]);
  useEffect(() => {
    if (!initialized.current) { initialized.current = true; return; }
    dirty.current = true;
    const timer = window.setTimeout(() => { dirty.current = false; saveRef.current(); }, 800);
    return () => window.clearTimeout(timer);
  }, [p.title, p.content, p.sourceKind, p.collectionId]);
  useEffect(() => () => { if (dirty.current) saveRef.current(); }, []);
  const sendAi = async () => {
    if (!aiPrompt.trim()) return;
    setAiWorking(true);
    try {
      const instruction = aiContext ? `${aiPrompt.trim()}\n\nSelected passage:\n> ${aiContext}` : aiPrompt.trim();
      await p.requestAi(instruction, aiFiles, () => undefined);
      setAiPrompt(''); setAiFiles([]); setAiContext('');
    } finally { setAiWorking(false); }
  };
  const askAboutSelection = (text: string, rect: DOMRect) => setSelectionAction({ text, left: Math.min(rect.left, window.innerWidth - 190), top: Math.min(rect.bottom + 8, window.innerHeight - 50) });
  const useSelection = () => { if (!selectionAction) return; setShowAi(true); setAiContext(selectionAction.text); setAiPrompt('Improve this selected passage'); setSelectionAction(null); };
  return <div className="flex min-h-0 flex-1 flex-col"><div className="flex flex-wrap items-center justify-between gap-3 pr-10"><div className="flex min-w-0 items-center gap-2 text-sm font-semibold"><button type="button" className={`btn btn-sm btn-square rounded-lg ${showAi ? 'btn-secondary' : 'btn-ghost border border-base-300'}`} onClick={() => setShowAi((value) => !value)} title="AI writing assistant" aria-label="Toggle AI writing assistant"><Sparkles size={16} /></button><span className="font-bold">Knowledge</span><span className="text-base-content/35">›</span>{writableScopes.length > 1 ? <OwnerSelector scopes={writableScopes} value={p.collectionId} onChange={p.setCollectionId} belongsTo={p.belongsTo} /> : <span>{owner}</span>}</div><div className="flex flex-wrap items-center gap-3"><span className="text-xs text-base-content/45">{p.busy ? 'Saving…' : 'Autosaved'}</span><DocumentControls status={p.status} onStatusChange={p.setStatus} sourceKind={p.sourceKind} onSourceKindChange={p.setSourceKind} /></div></div>
    <div className={`mt-4 grid min-h-0 flex-1 items-stretch gap-4 ${showAi ? 'lg:grid-cols-[21rem_minmax(0,1fr)]' : ''}`}>{showAi && <KnowledgeAiSidebar source={p.selected} prompt={aiPrompt} setPrompt={setAiPrompt} context={aiContext} clearContext={() => setAiContext('')} files={aiFiles} setFiles={setAiFiles} busy={p.busy} working={aiWorking} send={sendAi} disabled={isBinary} />}
      <div className="grid min-w-0 content-start gap-3 overflow-y-auto pr-1">{isBinary ? <div className="rounded-xl bg-base-200 p-4 text-sm">The original uploaded file is retained. Create a separate document when you need editable text.</div> : <div className={`grid min-w-0 items-start gap-3 ${pendingSuggestions.length > 0 ? 'xl:grid-cols-[minmax(0,1fr)_18rem]' : ''}`}><MarkdownEditor value={p.content} onChange={p.setContent} title={p.title} onTitleChange={p.setTitle} sourceKind={p.sourceKind} onSelectionAsk={askAboutSelection} proposalAnchors={pendingSuggestions.map((suggestion) => suggestionChangeRegion(suggestion).anchor).filter(Boolean)} proposalPreview={previewSuggestion && reviewPreview ? { suggestion: previewSuggestion, mode: reviewPreview.mode } : undefined} />{pendingSuggestions.length > 0 && <SuggestionCommentsRail suggestions={pendingSuggestions} busy={p.busy} accept={p.acceptAi} reject={p.rejectAi} preview={reviewPreview} onPreview={setReviewPreview} />}</div>}
      {p.selected.attachments.length > 0 && <details className="rounded-xl border border-base-300 p-4"><summary className="cursor-pointer font-bold">Attachments ({p.selected.attachments.length})</summary><ul className="mt-3 space-y-2 text-sm">{p.selected.attachments.map((a) => <li key={a.id} className="flex items-center gap-2"><File size={13} />{a.fileName}<span className="text-xs text-base-content/40">{new Date(a.createdAtUtc).toLocaleString()}</span></li>)}</ul></details>}
      {(p.selected.mimeType.startsWith('image/') || p.selected.mimeType === 'application/pdf') && <details className="rounded-xl border border-base-300 p-4"><summary className="cursor-pointer font-bold"><Map className="mr-2 inline" size={16} />Floor-plan graph tools</summary><div className="mt-4 flex gap-2"><input className="input input-bordered w-28" type="number" value={p.floorNumber} onChange={(e) => p.setFloorNumber(Number(e.target.value))} /><input className="input input-bordered flex-1" value={p.floorName} onChange={(e) => p.setFloorName(e.target.value)} /><button className="btn btn-accent" onClick={p.makeFloorPlan}>Generate</button></div>{p.floorDraft && <div className="mt-4"><textarea className="textarea textarea-bordered h-64 w-full font-mono text-xs" value={p.graphJson} onChange={(e) => p.setGraphJson(e.target.value)} /><div className="mt-2 flex gap-2"><button className="btn btn-outline btn-sm" onClick={() => p.run(async () => { const edited = JSON.parse(p.graphJson); p.setFloorDraft(await updateFloorPlanDraft({ ...p.floorDraft!, ...edited })); })}>Validate</button><button className="btn btn-primary btn-sm" onClick={() => p.run(async () => p.setFloorDraft(await publishFloorPlanDraft(p.floorDraft!.id)))}><Globe2 size={13} /> Publish graph</button></div></div>}</details>}
    </div></div>{selectionAction && <button type="button" className="fixed z-[120] flex items-center gap-2 rounded-lg border border-base-300 bg-base-100 px-3 py-2 text-xs font-semibold shadow-xl" style={{ left: selectionAction.left, top: selectionAction.top }} onMouseDown={(event) => event.preventDefault()} onClick={useSelection}><MessageSquarePlus size={14} className="text-secondary" />Ask AI about selection</button>}</div>;
}

function KnowledgeAiSidebar({ source, prompt, setPrompt, context, clearContext, files, setFiles, busy, working, send, disabled }: { source?: KnowledgeSourceDto; prompt: string; setPrompt: (value: string) => void; context?: string; clearContext?: () => void; files: File[]; setFiles: (files: File[]) => void; busy: boolean; working: boolean; send: () => Promise<void>; disabled: boolean }) {
  return <aside className="flex h-full min-h-0 flex-col overflow-hidden rounded-2xl border border-base-300 bg-base-100 shadow-sm"><div className="border-b border-base-300 px-5 py-4"><div className="flex items-center gap-2 text-sm font-bold"><span className="grid size-7 place-items-center rounded-lg bg-secondary/10 text-secondary"><Sparkles size={15} /></span>AI writing assistant</div><p className="mt-2 text-xs leading-5 text-base-content/50">Improve this knowledge. Review the single proposal comment before applying it.</p><div className="mt-3 flex items-center gap-2 rounded-lg bg-base-200/70 px-2.5 py-2 text-[10px] font-medium text-base-content/55"><File size={12} className="text-secondary" />Current editor content is included automatically</div></div><div className="min-h-0 flex-1 space-y-3 overflow-y-auto bg-base-200/25 p-4">
    {(!source || (source.conversation.length === 0 && source.suggestions.length === 0)) && <div className="rounded-xl border border-base-300 bg-base-100 p-4 text-xs leading-5 text-base-content/60">{source ? 'Ask AI to clarify, shorten, restructure or enrich this document. Attach evidence whenever new facts are needed.' : 'Describe the knowledge you want to create, or attach documents and images for AI to extract and enrich.'}</div>}
    {source?.conversation.map((turn) => <div key={`turn-${turn.sequence}`} className={`ai-message-enter rounded-xl px-3 py-2 text-xs leading-5 ${turn.role.toLowerCase() === 'user' ? 'ml-5 bg-secondary text-secondary-content' : 'mr-5 bg-base-200 text-base-content/70'}`}>{turn.message}</div>)}
    {working && <AiWorkingState instruction={prompt} />}
  </div><div className="shrink-0 border-t border-base-300 bg-base-100 p-4">{disabled ? <p className="rounded-xl bg-base-200 p-3 text-xs text-base-content/55">AI editing requires an editable Markdown document.</p> : <><div className="mb-3 flex flex-wrap gap-1.5">{['Improve clarity', 'Make it concise', 'Find missing details'].map((idea) => <button key={idea} type="button" className="rounded-full border border-base-300 bg-base-100 px-2.5 py-1 text-[11px] font-medium text-base-content/65 transition-colors hover:bg-base-200" onClick={() => setPrompt(idea)}>{idea}</button>)}</div>{context && <div className="mb-2 rounded-xl border border-secondary/20 bg-secondary/5 p-2.5"><div className="flex items-center justify-between gap-2 text-[10px] font-bold uppercase tracking-wide text-secondary"><span>Commenting on selection</span><button type="button" className="rounded p-0.5 hover:bg-base-200" onClick={clearContext} aria-label="Clear selected passage"><X size={12} /></button></div><p className="mt-1 line-clamp-3 text-[11px] leading-4 text-base-content/60">“{context}”</p></div>}{files.length > 0 && <div className="mb-2 flex items-center justify-between rounded-lg bg-base-200 px-3 py-2 text-[11px]"><span>{files.length} file(s) attached</span><button type="button" className="font-semibold text-base-content/55 hover:text-base-content" onClick={() => setFiles([])}>Clear</button></div>}<div className="rounded-xl border border-base-300 bg-base-100 p-2 shadow-sm focus-within:border-secondary/50"><textarea className="min-h-20 w-full resize-none bg-transparent px-1 text-sm leading-5 outline-none placeholder:text-base-content/35" value={prompt} onChange={(event) => setPrompt(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); void send(); } }} placeholder={context ? 'What should AI change in this passage?' : 'How should AI improve this content?'} /><div className="mt-1 flex items-center gap-2"><label className="grid size-8 cursor-pointer place-items-center rounded-lg text-base-content/50 hover:bg-base-200" title="Attach evidence"><Paperclip size={15} /><input hidden multiple type="file" onChange={(event) => setFiles(Array.from(event.target.files || []).slice(0, 8))} /></label><span className="mr-auto text-[10px] text-base-content/35">Ctrl + Enter</span><button className="grid size-8 place-items-center rounded-lg bg-secondary text-secondary-content transition-transform hover:scale-105 disabled:cursor-not-allowed disabled:opacity-35" disabled={busy || !prompt.trim()} onClick={() => void send()} aria-label="Suggest changes">{working ? <span className="loading loading-spinner loading-xs" /> : <Send size={14} />}</button></div></div></>}</div></aside>;
}

function AiWorkingState({ instruction }: { instruction: string }) {
  return <div className="ai-message-enter mr-3 rounded-xl border border-base-300 bg-base-100 p-3" aria-live="polite"><div className="flex items-center gap-2"><span className="loading loading-spinner loading-sm text-secondary" /><div className="min-w-0"><div className="text-xs font-bold">Preparing your review comment</div><p className="mt-1 truncate text-[11px] text-base-content/45">“{instruction}”</p></div></div></div>;
}

function suggestionChangeRegion(suggestion: KnowledgeSuggestionDto) {
  const before = suggestion.baseMarkdown.replace(/\r\n/g, '\n').split('\n');
  const after = suggestion.proposedMarkdown.replace(/\r\n/g, '\n').split('\n');
  let start = 0;
  while (start < before.length && start < after.length && before[start] === after[start]) start++;
  let beforeEnd = before.length - 1;
  let afterEnd = after.length - 1;
  while (beforeEnd >= start && afterEnd >= start && before[beforeEnd] === after[afterEnd]) { beforeEnd--; afterEnd--; }
  return {
    line: start + 1,
    before: before.slice(start, beforeEnd + 1).join('\n').trim(),
    after: after.slice(start, afterEnd + 1).join('\n').trim(),
    anchor: (before.slice(start, beforeEnd + 1).find((line) => line.trim()) || before[Math.max(0, start - 1)] || '').replace(/^\s*(?:#{1,6}|[-*+] |\d+\. )\s*/, '').replace(/[*_`]/g, '').trim(),
  };
}

function SuggestionCommentsRail({ suggestions, busy, accept, reject, preview, onPreview }: { suggestions: KnowledgeSuggestionDto[]; busy: boolean; accept: (id: string) => Promise<void>; reject: (id: string) => Promise<void>; preview: { id: string; mode: 'before' | 'after' } | null; onPreview: (value: { id: string; mode: 'before' | 'after' } | null) => void }) {
  return <aside className="sticky top-0 space-y-3" aria-label="AI proposal comments"><div className="flex items-center gap-2 px-1 text-xs font-bold"><MessageSquarePlus size={14} className="text-secondary" />Review comment</div>{suggestions.map((suggestion) => { const change = suggestionChangeRegion(suggestion); const mode = preview?.id === suggestion.id ? preview.mode : null; return <article key={suggestion.id} className="ai-message-enter rounded-xl border border-secondary/20 bg-base-100 p-3 shadow-sm"><div className="flex items-start gap-2"><span className="mt-0.5 size-2 shrink-0 rounded-full bg-secondary" /><div><div className="text-[10px] font-bold uppercase tracking-wide text-secondary">AI reasoning summary</div><p className="mt-1 text-xs leading-5 text-base-content/65">{suggestion.rationale}</p><div className="mt-1 text-[10px] text-base-content/35">Generated for this proposal · suggested near line {change.line}</div></div></div><div className="mt-3 flex rounded-lg bg-base-200 p-1"><button type="button" className={`flex-1 rounded-md px-2 py-1.5 text-[10px] font-bold ${mode === 'before' ? 'bg-base-100 shadow-sm' : 'text-base-content/45'}`} onClick={() => onPreview({ id: suggestion.id, mode: 'before' })}>Before</button><button type="button" className={`flex-1 rounded-md px-2 py-1.5 text-[10px] font-bold ${mode === 'after' ? 'bg-base-100 shadow-sm' : 'text-base-content/45'}`} onClick={() => onPreview({ id: suggestion.id, mode: 'after' })}>After</button>{mode && <button type="button" className="grid size-7 place-items-center rounded-md text-base-content/40 hover:bg-base-100" onClick={() => onPreview(null)} aria-label="Close preview"><X size={12} /></button>}</div><div className="mt-3 flex gap-2"><button className="btn btn-secondary btn-xs flex-1" disabled={busy} onClick={() => accept(suggestion.id)}><Check size={12} /> Apply</button><button className="btn btn-ghost btn-xs" disabled={busy} onClick={() => reject(suggestion.id)}>Dismiss</button></div></article>; })}</aside>;
}

function OwnerSelector({ scopes, value, onChange, belongsTo }: { scopes: KnowledgeCollectionDto[]; value: string; onChange: (value: string) => void; belongsTo: (scope?: KnowledgeCollectionDto) => string }) {
  const detailsRef = useRef<HTMLDetailsElement>(null);
  const mallWide = scopes.filter((scope) => scope.scopeType === 'Global');
  const tenants = scopes.filter((scope) => scope.scopeType === 'Tenant').sort((a, b) => belongsTo(a).localeCompare(belongsTo(b)));
  const current = scopes.find((scope) => scope.id === value);
  const choose = (id: string) => { onChange(id); detailsRef.current?.removeAttribute('open'); };
  const option = (scope: KnowledgeCollectionDto) => <li key={scope.id}><button type="button" className={`flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-left text-sm ${scope.id === value ? 'bg-base-200 font-semibold' : 'font-normal hover:bg-base-200/70'}`} onClick={() => choose(scope.id)}><span className="w-4">{scope.id === value && <Check size={13} />}</span><span className="truncate">{belongsTo(scope)}</span></button></li>;
  return <details ref={detailsRef} className="dropdown text-sm font-normal"><summary className="flex h-9 cursor-pointer list-none items-center gap-2 rounded-lg border border-base-300 bg-base-100 px-3 text-sm font-semibold hover:bg-base-200/60"><span className="max-w-40 truncate">{belongsTo(current)}</span><ChevronDown size={14} className="text-base-content/45" /></summary><ul className="dropdown-content z-30 mt-2 max-h-52 w-56 overflow-y-auto rounded-xl border border-base-300 bg-base-100 p-1.5 text-sm shadow-xl">{mallWide.map(option)}{tenants.length > 0 && <><li className="my-1.5 flex items-center gap-2 px-2 text-[9px] font-bold uppercase tracking-[.12em] text-base-content/40"><span className="h-px flex-1 bg-base-300" />Tenants<span className="h-px flex-1 bg-base-300" /></li>{tenants.map(option)}</>}</ul></details>;
}

function DocumentControls({ status, onStatusChange, sourceKind, onSourceKindChange }: { status: 'Draft' | 'Published'; onStatusChange: (value: 'Draft' | 'Published') => void; sourceKind: 'General' | 'Deal' | 'Event'; onSourceKindChange: (value: 'General' | 'Deal' | 'Event') => void }) {
  return <div className="flex items-end gap-2"><label className="grid gap-1"><span className="px-1 text-[10px] font-bold uppercase tracking-[.12em] text-base-content/45">Type</span><select className="select select-sm h-9 min-h-9 w-32 rounded-lg border-base-300 bg-base-100 text-sm font-semibold" value={sourceKind} onChange={(event) => onSourceKindChange(event.target.value as 'General' | 'Deal' | 'Event')} aria-label="Content type"><option value="General">General</option><option value="Deal">Deal</option><option value="Event">Event</option></select></label><label className="grid gap-1"><span className="px-1 text-[10px] font-bold uppercase tracking-[.12em] text-base-content/45">Status</span><select className={`select select-sm h-9 min-h-9 w-32 rounded-lg text-sm font-semibold ${status === 'Published' ? 'border-success/35 bg-success/10 text-success' : 'border-base-300 bg-base-100'}`} value={status} onChange={(event) => onStatusChange(event.target.value as 'Draft' | 'Published')} aria-label="Knowledge status"><option value="Draft">Draft</option><option value="Published">Published</option></select></label></div>;
}

function MarkdownEditor({ value, onChange, title, onTitleChange, sourceKind: _sourceKind = 'General', onSelectionAsk, proposalAnchors = [], proposalPreview, compact = false, placeholder = 'Start writing…' }: { value: string; onChange: (value: string) => void; title: string; onTitleChange: (value: string) => void; sourceKind?: 'General' | 'Deal' | 'Event'; onSelectionAsk?: (text: string, rect: DOMRect) => void; proposalAnchors?: string[]; proposalPreview?: { suggestion: KnowledgeSuggestionDto; mode: 'before' | 'after' }; compact?: boolean; placeholder?: string }) {
  const editorRef = useRef<HTMLDivElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const coverInputRef = useRef<HTMLInputElement>(null);
  const emittedValue = useRef<string | null>(null);
  const renderedKey = useRef('');
  const metadata = useRef('');
  const proposalAnchorKey = proposalAnchors.join('\u0000');
  const displayValue = proposalPreview ? (proposalPreview.mode === 'after' ? proposalPreview.suggestion.proposedMarkdown : proposalPreview.suggestion.baseMarkdown) : value;
  const importantDetails = importantFrontmatter(displayValue);
  const [editingDetail, setEditingDetail] = useState<{ key: string; label: string; value: string; isNew: boolean } | null>(null);
  const saveImportantDetail = () => {
    if (!editingDetail) return;
    const nextValue = editingDetail.value.trim();
    if (nextValue) onChange(upsertFrontmatterValue(value, editingDetail.key, nextValue));
    else if (!editingDetail.isNew) onChange(removeFrontmatterValue(value, editingDetail.key));
    setEditingDetail(null);
  };
  const removeImportantDetail = () => {
    if (editingDetail && !editingDetail.isNew) onChange(removeFrontmatterValue(value, editingDetail.key));
    setEditingDetail(null);
  };
  const captureSelection = () => {
    if (!onSelectionAsk || !editorRef.current) return;
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0 || selection.isCollapsed) return;
    const text = selection.toString().trim();
    const range = selection.getRangeAt(0);
    if (text.length < 2 || !editorRef.current.contains(range.commonAncestorContainer)) return;
    const rect = range.getBoundingClientRect();
    editorRef.current.querySelectorAll('mark[data-ai-selection]').forEach((mark) => mark.replaceWith(...Array.from(mark.childNodes)));
    try {
      const highlight = document.createElement('mark');
      highlight.dataset.aiSelection = 'true';
      highlight.appendChild(range.extractContents());
      range.insertNode(highlight);
    } catch { /* Complex browser selections still keep their quoted context. */ }
    onSelectionAsk(text.slice(0, 2_000), rect);
  };

  useEffect(() => {
    if (!editorRef.current) return;
    const nextRenderKey = `${proposalPreview ? `${proposalPreview.suggestion.id}:${proposalPreview.mode}` : 'current'}\u0000${displayValue}\u0000${proposalAnchorKey}`;
    if (renderedKey.current === nextRenderKey) return;
    if (!proposalPreview && value === emittedValue.current && renderedKey.current.startsWith('current\u0000')) { renderedKey.current = nextRenderKey; return; }
    renderedKey.current = nextRenderKey;
    const split = splitFrontmatter(displayValue);
    metadata.current = split.frontmatter;
    editorRef.current.innerHTML = markdownToEditorHtml(removeDuplicateTitleHeading(split.body, title));
    const previewChange = proposalPreview ? suggestionChangeRegion(proposalPreview.suggestion) : null;
    const previewAnchors = previewChange ? markdownFragmentAnchors(proposalPreview!.mode === 'after' ? previewChange.after : previewChange.before || previewChange.anchor) : proposalAnchors;
    previewAnchors.forEach((anchor) => highlightTextNode(editorRef.current!, anchor));
  }, [displayValue, proposalAnchorKey, proposalPreview?.suggestion.id, proposalPreview?.mode]);

  const sync = () => {
    if (!editorRef.current) return;
    const markdown = `${metadata.current}${editorHtmlToMarkdown(editorRef.current)}`.trimEnd();
    emittedValue.current = markdown;
    onChange(markdown);
  };
  const command = (name: string, argument?: string) => {
    editorRef.current?.focus();
    document.execCommand(name, false, argument);
    sync();
  };
  const createLink = () => {
    const href = window.prompt('Link URL');
    if (href && /^(https?:|mailto:)/i.test(href)) command('createLink', href);
  };
  const insertImage = (file?: File) => {
    if (!file) return;
    if (!file.type.startsWith('image/')) { window.alert('Choose an image file.'); return; }
    if (file.size > 10 * 1024 * 1024) { window.alert('Images must be 10 MB or smaller.'); return; }
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result !== 'string') return;
      editorRef.current?.focus();
      document.execCommand('insertImage', false, reader.result);
      const images = editorRef.current?.querySelectorAll('img');
      const inserted = images?.item((images.length || 1) - 1);
      if (inserted) inserted.setAttribute('alt', file.name);
      sync();
    };
    reader.readAsDataURL(file);
  };
  const coverImage = displayValue.match(/!\[[^\]]*]\((data:image\/[^)]+|https?:\/\/[^)\s]+|\/[^)\s]+)\)/i)?.[1];
  const setCoverImage = (file?: File) => {
    if (!file) return;
    if (!file.type.startsWith('image/')) { window.alert('Choose an image file.'); return; }
    if (file.size > 10 * 1024 * 1024) { window.alert('Images must be 10 MB or smaller.'); return; }
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result !== 'string') return;
      const split = splitFrontmatter(value);
      const body = split.body.replace(/!\[[^\]]*]\((data:image\/[^)]+|https?:\/\/[^)\s]+|\/[^)\s]+)\)\s*/i, '').trimStart();
      onChange(`${split.frontmatter}![${title || 'Cover image'}](${reader.result})\n\n${body}`.trimEnd());
    };
    reader.readAsDataURL(file);
  };
  return <section className="overflow-hidden rounded-2xl border border-base-300 bg-base-200/35">
    <div className="sticky top-0 z-10 flex flex-wrap items-center gap-1 border-b border-base-300 bg-base-100 px-3 py-2">
      <EditorButton label="Heading 1" onClick={() => command('formatBlock', 'h1')}><Heading1 size={16} /></EditorButton>
      <EditorButton label="Heading 2" onClick={() => command('formatBlock', 'h2')}><Heading2 size={16} /></EditorButton>
      <span className="mx-1 h-5 w-px bg-base-300" />
      <EditorButton label="Bold" onClick={() => command('bold')}><Bold size={16} /></EditorButton>
      <EditorButton label="Italic" onClick={() => command('italic')}><Italic size={16} /></EditorButton>
      <EditorButton label="Link" onClick={createLink}><Link size={16} /></EditorButton>
      <EditorButton label="Upload image" onClick={() => imageInputRef.current?.click()}><ImagePlus size={16} /></EditorButton>
      <input ref={imageInputRef} className="hidden" type="file" accept="image/png,image/jpeg,image/webp,image/gif" onChange={(event) => { insertImage(event.target.files?.[0]); event.target.value = ''; }} />
      <span className="mx-1 h-5 w-px bg-base-300" />
      <EditorButton label="Bulleted list" onClick={() => command('insertUnorderedList')}><List size={16} /></EditorButton>
      <EditorButton label="Numbered list" onClick={() => command('insertOrderedList')}><ListOrdered size={16} /></EditorButton>
      <EditorButton label="Quote" onClick={() => command('formatBlock', 'blockquote')}><Quote size={16} /></EditorButton>
      <EditorButton label="Normal text" onClick={() => command('formatBlock', 'p')}><span className="px-1 text-xs font-semibold">Text</span></EditorButton>
    </div>
    <div className="p-3 sm:p-6">
      <div className="mx-auto max-w-3xl rounded-xl bg-base-100 px-7 py-8 shadow-sm sm:px-12 sm:py-10">
        {proposalPreview && <div className="mb-5 flex items-center gap-2 rounded-xl border border-secondary/25 bg-secondary/5 px-3 py-2 text-xs font-semibold text-secondary"><Sparkles size={14} />{proposalPreview.mode === 'after' ? 'After · proposed document preview' : 'Before · current document preview'}</div>}
        <input readOnly={Boolean(proposalPreview)} className="!h-auto !w-full !border-0 !bg-transparent !p-0 !text-3xl !font-bold !leading-tight tracking-[-0.035em] !outline-none placeholder:text-base-content/25 sm:!text-4xl" value={title} onChange={(event) => onTitleChange(event.target.value)} placeholder="Untitled document" />
        {!proposalPreview && <div className="mt-5"><label className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-base-300 bg-base-100 px-3 py-2 text-xs font-semibold text-base-content/65 hover:bg-base-200">{coverImage ? <img src={coverImage} alt="" className="size-8 rounded-md object-cover" /> : <span className="grid size-8 place-items-center rounded-md bg-base-200"><ImagePlus size={15} /></span>}<span>{coverImage ? 'Change cover image' : 'Add cover image'}</span><input ref={coverInputRef} hidden type="file" accept="image/png,image/jpeg,image/webp,image/gif" onChange={(event) => { setCoverImage(event.target.files?.[0]); event.target.value = ''; }} /></label></div>}
        <div className="mt-5 flex flex-wrap gap-2">{importantDetails.filter((detail) => editingDetail?.key !== detail.key).map((detail) => <button type="button" key={detail.key} className="group flex min-h-7 items-center gap-1.5 rounded-full border border-base-300 bg-base-200/55 px-3 py-1 text-xs hover:border-base-content/25 hover:bg-base-200" onClick={() => setEditingDetail({ ...detail, isNew: false })} title={`Edit ${detail.label.toLowerCase()}`}><span className="font-bold text-base-content/50">{detail.label}</span><span className="max-w-64 truncate font-medium text-base-content/75">{detail.value}</span><Edit3 size={11} className="ml-0.5 text-base-content/25 group-hover:text-base-content/50" /></button>)}{editingDetail && <div className="flex min-h-8 items-center gap-1 rounded-full border border-secondary/45 bg-base-100 py-1 pl-3 pr-1 shadow-sm"><label className="text-xs font-bold text-base-content/50" htmlFor={`metadata-${editingDetail.key}`}>{editingDetail.label}</label><input id={`metadata-${editingDetail.key}`} autoFocus type={isDateMetadata(editingDetail.key) ? 'date' : 'text'} className="h-6 min-w-28 max-w-56 bg-transparent px-1 text-xs font-medium outline-none" value={editingDetail.value} onChange={(event) => setEditingDetail({ ...editingDetail, value: event.target.value })} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); saveImportantDetail(); } if (event.key === 'Escape') setEditingDetail(null); }} placeholder={`Enter ${editingDetail.label.toLowerCase()}`} /><button type="button" className="grid size-6 place-items-center rounded-full text-success hover:bg-success/10" onClick={saveImportantDetail} aria-label="Save detail"><Check size={12} /></button><button type="button" className="grid size-6 place-items-center rounded-full text-base-content/40 hover:bg-base-200 hover:text-error" onClick={removeImportantDetail} aria-label={editingDetail.isNew ? 'Cancel' : 'Remove detail'}>{editingDetail.isNew ? <X size={12} /> : <Trash2 size={12} />}</button></div>}<details className="dropdown"><summary className="flex min-h-7 cursor-pointer list-none items-center gap-1.5 rounded-full border border-dashed border-base-300 px-3 py-1 text-xs font-medium text-base-content/45 hover:border-base-content/35 hover:bg-base-200/50 hover:text-base-content/65"><Plus size={12} />{importantDetails.length ? 'Add detail' : 'Add important details'}</summary><ul className="dropdown-content z-20 mt-2 w-48 rounded-xl border border-base-300 bg-base-100 p-1.5 shadow-lg">{ImportantMetadataChoices.filter((choice) => !importantDetails.some((detail) => detail.key === choice.key)).map((choice) => <li key={choice.key}><button type="button" className="w-full rounded-lg px-3 py-2 text-left text-xs font-medium hover:bg-base-200" onClick={(event) => { setEditingDetail({ ...choice, value: '', isNew: true }); event.currentTarget.closest('details')?.removeAttribute('open'); }}>{choice.label}</button></li>)}</ul></details></div>
        <div className="my-7 border-t border-base-200" />
        <div
          ref={editorRef}
          contentEditable={!proposalPreview}
          suppressContentEditableWarning
          data-placeholder={placeholder}
          onInput={() => { if (!proposalPreview) sync(); }}
          onBlur={() => { if (!proposalPreview) sync(); }}
          onMouseUp={captureSelection}
          onKeyUp={(event) => { if (event.shiftKey && ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) captureSelection(); }}
          className={`text-[15px] leading-7 outline-none empty:before:pointer-events-none empty:before:text-base-content/35 empty:before:content-[attr(data-placeholder)] [&_a]:text-primary [&_a]:underline [&_blockquote]:my-4 [&_blockquote]:border-l-3 [&_blockquote]:border-base-300 [&_blockquote]:pl-4 [&_blockquote]:text-base-content/65 [&_code]:rounded [&_code]:bg-base-200 [&_code]:px-1 [&_h1]:mb-4 [&_h1]:mt-6 [&_h1]:text-3xl [&_h1]:font-bold [&_h2]:mb-3 [&_h2]:mt-5 [&_h2]:text-2xl [&_h2]:font-bold [&_h3]:mb-2 [&_h3]:mt-4 [&_h3]:text-xl [&_h3]:font-bold [&_img]:my-5 [&_img]:max-h-[32rem] [&_img]:max-w-full [&_img]:rounded-xl [&_img]:object-contain [&_li]:my-1 [&_ol]:my-3 [&_ol]:list-decimal [&_ol]:pl-7 [&_p]:my-3 [&_ul]:my-3 [&_ul]:list-disc [&_ul]:pl-7 ${compact ? 'min-h-48' : 'min-h-[28rem]'}`}
        />
      </div>
    </div>
  </section>;
}

function EditorButton({ label, onClick, children }: { label: string; onClick: () => void; children: ReactNode }) {
  return <button type="button" className="btn btn-ghost btn-sm px-2" title={label} aria-label={label} onMouseDown={(event) => event.preventDefault()} onClick={onClick}>{children}</button>;
}

function splitFrontmatter(markdown: string) {
  const match = markdown.match(/^(---\s*\r?\n[\s\S]*?\r?\n---\s*\r?\n?)([\s\S]*)$/);
  return { frontmatter: match?.[1] || '', body: match?.[2] ?? markdown };
}

const ImportantMetadataLabels: Record<string, string> = {
    available_from: 'Starts', valid_from: 'Starts', start_date: 'Starts', event_date: 'Date', date: 'Date',
    available_until: 'Ends', valid_until: 'Ends', end_date: 'Ends', time: 'Time',
    price_range: 'Price', price: 'Price',
    location: 'Location', venue: 'Venue', opening_hours: 'Hours', hours: 'Hours', contact: 'Contact',
};
const ImportantMetadataChoices = [
  { key: 'available_from', label: 'Start date' }, { key: 'available_until', label: 'End date' },
  { key: 'price_range', label: 'Price range' }, { key: 'location', label: 'Location' },
  { key: 'opening_hours', label: 'Opening hours' }, { key: 'contact', label: 'Contact' },
];

function isDateMetadata(key: string) {
  return ['available_from', 'available_until', 'valid_from', 'valid_until', 'start_date', 'end_date', 'event_date', 'date'].includes(key);
}

function importantFrontmatter(markdown: string) {
  const values = parseFrontmatterValues(markdown);
  return Object.entries(values).filter(([key, value]) => ImportantMetadataLabels[key] && value).slice(0, 8)
    .map(([key, value]) => ({ key, label: ImportantMetadataLabels[key], value }));
}

function parseFrontmatterValues(markdown: string) {
  const frontmatter = splitFrontmatter(markdown).frontmatter;
  if (!frontmatter) return {} as Record<string, string>;
  const values: Record<string, string> = {};
  let listKey = '';
  for (const line of frontmatter.split(/\r?\n/).slice(1, -1)) {
    const property = line.match(/^([A-Za-z][\w-]*):\s*(.*)$/);
    if (property) {
      listKey = property[1].toLowerCase().replaceAll('-', '_');
      const value = property[2].trim().replace(/^['"]|['"]$/g, '').replace(/^\[(.*)\]$/, '$1');
      if (value) values[listKey] = value;
      continue;
    }
    const item = line.match(/^\s*-\s*(.+)$/);
    if (item && listKey) {
      const value = item[1].trim().replace(/^['"]|['"]$/g, '');
      values[listKey] = [values[listKey], value].filter(Boolean).join(', ');
    }
  }
  return values;
}

const DescriptiveMetadataBodyFields: Record<string, { heading: string; label?: string }> = {
  audience: { heading: 'Who this is for' },
  target_audience: { heading: 'Who this is for' },
  occasion: { heading: 'When this is relevant' },
  occasions: { heading: 'When this is relevant' },
  dietary: { heading: 'Important conditions', label: 'Dietary suitability' },
  dietary_suitability: { heading: 'Important conditions', label: 'Dietary suitability' },
  gift_purpose: { heading: 'Purpose' },
  purpose: { heading: 'Purpose' },
  product_interests: { heading: 'Relevant interests' },
  interests: { heading: 'Relevant interests' },
};

/** Move shopper-facing legacy YAML into readable Markdown while keeping operational data as chips. */
function promoteDescriptiveMetadataToBody(markdown: string) {
  const values = parseFrontmatterValues(markdown);
  const descriptive = Object.entries(values).filter(([key, value]) => DescriptiveMetadataBodyFields[key] && value);
  if (!descriptive.length) return markdown;

  let next = markdown;
  const additions = new globalThis.Map<string, string[]>();
  const visibleBody = splitFrontmatter(markdown).body.toLocaleLowerCase();
  for (const [key, value] of descriptive) {
    next = removeFrontmatterValue(next, key);
    if (visibleBody.includes(value.toLocaleLowerCase())) continue;
    const field = DescriptiveMetadataBodyFields[key];
    const line = field.label ? `- **${field.label}:** ${value}` : value;
    additions.set(field.heading, [...(additions.get(field.heading) || []), line]);
  }

  next = removeEmptyFrontmatter(next).trimEnd();
  const sections = [...additions.entries()].map(([heading, lines]) => `## ${heading}\n\n${lines.join('\n')}`);
  return sections.length ? `${next}${next ? '\n\n' : ''}${sections.join('\n\n')}\n` : next;
}

function removeEmptyFrontmatter(markdown: string) {
  const split = splitFrontmatter(markdown);
  if (!split.frontmatter) return markdown;
  const meaningful = split.frontmatter.split(/\r?\n/).slice(1, -1).some((line) => line.trim().length > 0);
  return meaningful ? markdown : split.body.replace(/^\s+/, '');
}

function upsertFrontmatterValue(markdown: string, key: string, value: string) {
  const split = splitFrontmatter(markdown);
  const serialized = JSON.stringify(value);
  if (!split.frontmatter) return `---\n${key}: ${serialized}\n---\n\n${markdown}`;
  const lines = split.frontmatter.trimEnd().split(/\r?\n/);
  const property = new RegExp(`^${metadataKeyPattern(key)}:`, 'i');
  const index = lines.findIndex((line) => property.test(line));
  if (index >= 0) lines[index] = `${key}: ${serialized}`;
  else lines.splice(lines.length - 1, 0, `${key}: ${serialized}`);
  return `${lines.join('\n')}\n${split.body}`;
}

function removeFrontmatterValue(markdown: string, key: string) {
  const split = splitFrontmatter(markdown);
  if (!split.frontmatter) return markdown;
  const lines = split.frontmatter.trimEnd().split(/\r?\n/);
  const property = new RegExp(`^${metadataKeyPattern(key)}:`, 'i');
  const index = lines.findIndex((line) => property.test(line));
  if (index < 0) return markdown;
  let end = index + 1;
  while (end < lines.length - 1 && /^\s+-\s+/.test(lines[end])) end++;
  lines.splice(index, end - index);
  return `${lines.join('\n')}\n${split.body}`;
}

function metadataKeyPattern(key: string) {
  return key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replaceAll('_', '[-_]');
}

function removeDuplicateTitleHeading(markdown: string, title: string) {
  if (!title.trim()) return markdown;
  const lines = markdown.split(/\r?\n/);
  const firstContent = lines.findIndex((line) => line.trim().length > 0);
  if (firstContent < 0) return markdown;
  const heading = lines[firstContent].match(/^#\s+(.+)$/);
  const normalize = (value: string) => value.trim().replace(/\s+/g, ' ').toLocaleLowerCase();
  if (!heading || normalize(heading[1]) !== normalize(title)) return markdown;
  lines.splice(firstContent, 1);
  if (lines[firstContent]?.trim() === '') lines.splice(firstContent, 1);
  return lines.join('\n');
}

function markdownToEditorHtml(markdown: string) {
  const lines = markdown.split(/\r?\n/);
  const output: string[] = [];
  let list: 'ul' | 'ol' | null = null;
  const closeList = () => { if (list) output.push(`</${list}>`); list = null; };
  for (const rawLine of lines) {
    const line = rawLine.trimEnd();
    const bullet = line.match(/^\s*[-*]\s+(.+)$/);
    const numbered = line.match(/^\s*\d+\.\s+(.+)$/);
    if (bullet || numbered) {
      const nextList = bullet ? 'ul' : 'ol';
      if (list !== nextList) { closeList(); list = nextList; output.push(`<${list}>`); }
      output.push(`<li>${inlineMarkdownToHtml((bullet || numbered)![1])}</li>`);
      continue;
    }
    closeList();
    if (!line.trim()) { output.push('<p><br></p>'); continue; }
    const heading = line.match(/^(#{1,3})\s+(.+)$/);
    if (heading) { const level = heading[1].length; output.push(`<h${level}>${inlineMarkdownToHtml(heading[2])}</h${level}>`); continue; }
    if (line.startsWith('> ')) { output.push(`<blockquote>${inlineMarkdownToHtml(line.slice(2))}</blockquote>`); continue; }
    if (/^---+$/.test(line.trim())) { output.push('<hr>'); continue; }
    output.push(`<p>${inlineMarkdownToHtml(line)}</p>`);
  }
  closeList();
  return output.join('');
}

function inlineMarkdownToHtml(value: string) {
  const escaped = escapeHtml(value);
  return escaped
    .replace(/!\[([^\]]*)\]\((data:image\/[^\s)]+|https?:\/\/[^\s)]+)\)/g, '<img src="$2" alt="$1">')
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\*([^*]+)\*/g, '<em>$1</em>')
    .replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+|mailto:[^\s)]+)\)/g, '<a href="$2">$1</a>');
}

function escapeHtml(value: string) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

function highlightTextNode(root: HTMLElement, rawAnchor: string) {
  const anchor = rawAnchor.trim().slice(0, 180);
  if (anchor.length < 4) return;
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  let node = walker.nextNode();
  while (node) {
    const value = node.textContent || '';
    const index = value.toLocaleLowerCase().indexOf(anchor.toLocaleLowerCase());
    if (index >= 0 && node.parentNode) {
      const textNode = node as Text;
      const mark = document.createElement('mark');
      mark.dataset.aiProposal = 'true';
      mark.title = 'AI has attached a proposal comment to this passage';
      const after = textNode.splitText(index);
      after.splitText(anchor.length);
      node.parentNode.replaceChild(mark, after);
      mark.appendChild(after);
      return;
    }
    node = walker.nextNode();
  }
}

function markdownFragmentAnchors(fragment: string) {
  return fragment.split(/\r?\n/)
    .map((line) => line.replace(/^\s*(?:#{1,6}|[-*+] |\d+\. )\s*/, '').replace(/[*_`]/g, '').trim())
    .filter((line) => line.length >= 4)
    .slice(0, 12);
}

function editorHtmlToMarkdown(root: HTMLElement) {
  const inline = (node: Node): string => {
    if (node.nodeType === Node.TEXT_NODE) return node.textContent || '';
    if (!(node instanceof HTMLElement)) return '';
    const content = Array.from(node.childNodes).map(inline).join('');
    const tag = node.tagName.toLowerCase();
    if (tag === 'strong' || tag === 'b') return `**${content}**`;
    if (tag === 'em' || tag === 'i') return `*${content}*`;
    if (tag === 'code') return `\`${content}\``;
    if (tag === 'img') return `![${node.getAttribute('alt') || 'Image'}](${node.getAttribute('src') || ''})`;
    if (tag === 'a') return `[${content}](${node.getAttribute('href') || ''})`;
    if (tag === 'br') return '\n';
    return content;
  };
  const block = (node: Node): string => {
    if (node.nodeType === Node.TEXT_NODE) return node.textContent || '';
    if (!(node instanceof HTMLElement)) return '';
    const tag = node.tagName.toLowerCase();
    if (tag === 'ul' || tag === 'ol') return Array.from(node.children).map((item, index) => `${tag === 'ol' ? `${index + 1}.` : '-'} ${inline(item)}`).join('\n') + '\n\n';
    if (tag === 'h1') return `# ${inline(node)}\n\n`;
    if (tag === 'h2') return `## ${inline(node)}\n\n`;
    if (tag === 'h3') return `### ${inline(node)}\n\n`;
    if (tag === 'blockquote') return `> ${inline(node)}\n\n`;
    if (tag === 'hr') return '---\n\n';
    if (tag === 'p' || tag === 'div') return `${inline(node)}\n\n`;
    return inline(node);
  };
  return Array.from(root.childNodes).map(block).join('').replace(/\n{3,}/g, '\n\n').trim();
}

