import React, { useEffect, useState } from 'react';
import { Bot, ImageIcon, Navigation, Search, Sparkles } from 'lucide-react';
import { getAllStores, searchStorefront } from '../api';
import type { StoreDto, StorefrontSearchResponse } from '../types';
import { AgentMarkdown } from './AgentMarkdown';

interface DirectoryViewProps {
  onNavigateToStore: (store: StoreDto) => void;
  onAskConcierge: (query: string) => void;
  onAskAiPage: (query: string) => void;
}

const categories = [
  { id: 'all', label: 'All' }, { id: 1, label: 'Fashion' },
  { id: 2, label: 'Electronics' }, { id: 3, label: 'Food & dining' },
  { id: 4, label: 'Entertainment' }, { id: 5, label: 'Wellness' },
  { id: 8, label: 'Services' },
];

export const DirectoryView: React.FC<DirectoryViewProps> = ({ onNavigateToStore, onAskConcierge, onAskAiPage }) => {
  const [stores, setStores] = useState<StoreDto[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [submittedQuery, setSubmittedQuery] = useState('');
  const [selectedFloor, setSelectedFloor] = useState<number | 'all'>('all');
  const [selectedCategory, setSelectedCategory] = useState<number | 'all'>('all');
  const [loading, setLoading] = useState(true);
  const [agentResult, setAgentResult] = useState<StorefrontSearchResponse | null>(null);
  const [agentLoading, setAgentLoading] = useState(false);

  useEffect(() => {
    getAllStores().then(setStores)
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { setAgentResult(null); }, [submittedQuery, selectedFloor]);

  const runAiSearch = () => {
    const query = submittedQuery.trim();
    if (query.length < 2 || agentLoading) return;
    setAgentLoading(true);
    void searchStorefront(query, selectedFloor === 'all' ? undefined : selectedFloor, 'Directory')
      .then(setAgentResult).catch(() => setAgentResult(null)).finally(() => setAgentLoading(false));
  };

  const filteredStores = stores.filter((store) => {
    const query = submittedQuery.trim().toLowerCase();
    const agentIds = new Set(agentResult?.stores.map((item) => item.tenantId) || []);
    return (selectedFloor === 'all' || store.floorNumber === selectedFloor)
      && (selectedCategory === 'all' || store.category === selectedCategory)
      && (!query || agentIds.has(store.id) || store.name.toLowerCase().includes(query) || store.description.toLowerCase().includes(query) || store.unitNumber.toLowerCase().includes(query));
  }).sort((left, right) => {
    if (!submittedQuery.trim() || !agentResult) return 0;
    const ranks = new globalThis.Map(agentResult.stores.map((item, index) => [item.tenantId, index]));
    return (ranks.get(left.id) ?? Number.MAX_SAFE_INTEGER) - (ranks.get(right.id) ?? Number.MAX_SAFE_INTEGER);
  });
  const hasAgentMatches = Boolean(agentResult && (agentResult.stores.length > 0 || agentResult.knowledge.length > 0));

  return (
    <div className="space-y-9">
      <section className="grid gap-6 lg:grid-cols-[.8fr_1.2fr] lg:items-end">
        <div><h2 className="text-3xl font-bold tracking-[-0.04em] md:text-4xl">Find a store</h2><p className="mt-2 max-w-xl text-sm leading-6 text-base-content/55">Search stores, restaurants and services across the mall.</p></div>
        <form className="surface-card flex min-h-16 items-center gap-3 px-5 focus-within:border-primary/50" onSubmit={(event) => { event.preventDefault(); setAgentResult(null); setSubmittedQuery(searchQuery.trim()); }}><Search size={20} className="shrink-0 text-primary" /><input className="w-full bg-transparent text-sm outline-none placeholder:text-base-content/35" type="search" placeholder="Search stores, products, services or facilities" value={searchQuery} onChange={(event) => setSearchQuery(event.target.value)} /><button className="btn btn-primary btn-sm rounded-full" disabled={searchQuery.trim().length < 2} type="submit">Search</button></form>
      </section>

      <section className="flex flex-wrap items-center justify-between gap-4 border-y border-base-300/80 py-4">
        <div className="flex flex-wrap items-center gap-2">{categories.map((category) => <button key={category.id} onClick={() => setSelectedCategory(category.id as number | 'all')} className={`rounded-full px-4 py-2 text-xs font-semibold transition ${selectedCategory === category.id ? 'bg-primary text-primary-content' : 'bg-base-100 text-base-content/55'}`}>{category.label}</button>)}</div>
        <div className="flex flex-wrap items-center gap-2">{[{ id: 'all', label: 'All floors' }, { id: 1, label: 'Ground' }, { id: 2, label: 'Level 2' }, { id: 3, label: 'Level 3' }].map((floor) => <button key={floor.id} onClick={() => setSelectedFloor(floor.id as number | 'all')} className={`rounded-full px-4 py-2 text-xs font-semibold transition ${selectedFloor === floor.id ? 'bg-neutral text-neutral-content' : 'bg-base-100 text-base-content/55'}`}>{floor.label}</button>)}</div>
        <span className="text-xs text-base-content/45">Showing {filteredStores.length} destinations</span>
      </section>

      {agentLoading && <section className="surface-card p-5" aria-live="polite"><div className="flex items-start gap-3"><span className="relative mt-0.5 grid size-8 shrink-0 place-items-center rounded-full bg-primary/10 text-primary"><Sparkles size={16} /><span className="absolute inset-0 animate-ping rounded-full border border-primary/20" /></span><div className="min-w-0 flex-1"><div className="text-sm font-semibold">Searching…</div><div className="mt-3 space-y-2"><div className="h-2.5 w-3/4 animate-pulse rounded-full bg-base-300/70" /><div className="h-2.5 w-1/2 animate-pulse rounded-full bg-base-300/50" /></div></div></div></section>}

      {!agentLoading && agentResult && <section className="surface-card p-5" aria-live="polite"><div className="flex items-start gap-3"><span className="grid size-8 shrink-0 place-items-center rounded-full bg-primary/10 text-primary"><Sparkles size={16} /></span><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center justify-between gap-3"><h3 className="text-sm font-semibold">{hasAgentMatches ? 'Suggestions for you' : 'Ask the Sunway Kluang Mall AI'}</h3><button type="button" className="btn btn-ghost btn-sm rounded-full border border-base-300" onClick={() => onAskAiPage(submittedQuery)}><Bot size={14} />{hasAgentMatches ? 'Ask more' : 'Continue in Ask AI'}</button></div><AgentMarkdown>{hasAgentMatches ? agentResult.answer : 'This request needs a conversation rather than a directory search. Continue in Ask AI and your question will be carried over.'}</AgentMarkdown></div></div></section>}

      {!agentLoading && agentResult && agentResult.resultFocus !== 'Stores' && agentResult.knowledge.length > 0 && <section><div className="mb-3 flex items-center gap-2"><Sparkles size={15} className="text-primary" /><h3 className="text-sm font-semibold">Related deals and information</h3></div><div className="grid gap-3 sm:grid-cols-2">{agentResult.knowledge.slice(0, 4).map((item) => <article key={item.sourceId} className="surface-card p-4"><div className="text-sm font-semibold">{item.title}</div><p className="mt-1 line-clamp-2 text-xs leading-5 text-base-content/50">{item.snippet}</p></article>)}</div></section>}

      {loading ? <div className="py-20 text-center text-base-content/45"><span className="loading loading-spinner loading-md" /><p className="mt-3">Loading directory...</p></div> : filteredStores.length === 0 && submittedQuery && !agentResult ? <div className="surface-card py-12 text-center"><p className="text-base-content/60">No stores match “{submittedQuery}”.</p></div> : filteredStores.length === 0 && agentResult ? null : filteredStores.length === 0 ? <div className="surface-card py-20 text-center text-base-content/50">No stores matched your filters. Try another search or category.</div> : (
        <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
          {filteredStores.map((store) => {
            const image = store.storefrontImageUrl;
            const recommendation = agentResult?.stores.find((item) => item.tenantId === store.id);
            return <article key={store.id} className="surface-card flex min-h-72 flex-col overflow-hidden">
              <div className="aspect-[16/9] overflow-hidden bg-base-200/70">{image ? <img src={image} alt="" className="h-full w-full object-cover" loading="lazy" /> : <div className="grid h-full place-items-center text-base-content/20"><ImageIcon size={30} strokeWidth={1.4} /></div>}</div>
              <div className="flex flex-1 flex-col p-5"><div className="flex items-center justify-between gap-4"><h3 className="text-xl font-bold tracking-tight">{store.name}</h3><span className="rounded-full bg-base-200 px-3 py-1 text-[11px] font-bold uppercase tracking-wider">{store.unitNumber}</span></div>
              {submittedQuery && recommendation?.whyRecommended && <p className="mt-4 line-clamp-2 text-sm leading-6 text-base-content/65">{recommendation.whyRecommended}</p>}
              <div className="mt-auto border-t border-base-300/75 pt-4"><div className="flex gap-2"><button onClick={() => onNavigateToStore(store)} className="btn btn-primary btn-sm flex-1 rounded-full"><Navigation size={13} /> Directions</button><button onClick={() => onAskConcierge(`Tell me about ${store.name} and what they offer`)} className="btn btn-ghost btn-sm rounded-full border border-base-300"><Sparkles size={13} /> Ask</button></div></div></div>
            </article>;
          })}
        </div>
      )}
      {!loading && submittedQuery && !agentResult && <div className="surface-card flex flex-wrap items-center justify-center gap-3 p-5 text-center"><p className="text-sm text-base-content/60">Not seeing what you’re looking for?</p><button className="btn btn-primary btn-sm rounded-full" disabled={agentLoading} onClick={runAiSearch}><Sparkles size={14} />{agentLoading ? 'Searching with AI…' : 'Try AI search'}</button></div>}
    </div>
  );
};
