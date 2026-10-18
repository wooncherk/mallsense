import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Bot, ChevronLeft, ChevronRight, ImageIcon, MapPin, Navigation, Search, Sparkles, X } from 'lucide-react';
import { getAllStores, getPublishedKnowledgeSources, searchStorefront, trackShopperBehavior } from '../api';
import type { PublicKnowledgeSourceDto, ShopperBehaviorEventInput, StoreDto, StorefrontSearchResponse } from '../types';
import { AgentMarkdown } from './AgentMarkdown';

interface DealsEventsViewProps {
  onNavigateToStore: (store: StoreDto) => void;
  onAskConcierge: (query: string) => void;
  onAskAiPage: (query: string) => void;
}

interface OfferDocument {
  source: PublicKnowledgeSourceDto;
  tenantId?: string;
  tenant?: string;
  thumbnailUrl?: string;
  searchableText: string;
}

type OfferFilter = 'All' | 'Deal' | 'Event';

function cleanMetadataValue(value?: string) {
  return value?.trim().replace(/^['"]|['"]$/g, '');
}

function plainText(markdown: string) {
  return markdown
    .replace(/!\[[^\]]*]\([^)]*\)/g, '')
    .replace(/\[([^\]]+)]\([^)]*\)/g, '$1')
    .replace(/^#{1,6}\s+/gm, '')
    .replace(/^[-*+]\s+/gm, '')
    .replace(/^>\s?/gm, '')
    .replace(/[*_`~]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function parseOffer(source: PublicKnowledgeSourceDto): OfferDocument {
  const markdown = source.markdownContent.trim();
  const match = markdown.match(/^---\s*\r?\n([\s\S]*?)\r?\n---\s*(?:\r?\n)?([\s\S]*)$/);
  const metadata = new globalThis.Map<string, string>();
  if (match) {
    match[1].split(/\r?\n/).forEach((line) => {
      const separator = line.indexOf(':');
      if (separator > 0) metadata.set(line.slice(0, separator).trim().toLowerCase().replaceAll('-', '_'), cleanMetadataValue(line.slice(separator + 1)) || '');
    });
  }
  const body = (match?.[2] ?? markdown).replace(/^#\s+.*(?:\r?\n)+/, '').trim();
  const thumbnail = body.match(/!\[[^\]]*]\((data:image\/[^)]+|https?:\/\/[^)\s]+|\/[^)\s]+)\)/i)?.[1];
  return {
    source,
    tenantId: metadata.get('tenant_id'),
    tenant: metadata.get('tenant'),
    thumbnailUrl: thumbnail,
    searchableText: plainText(body),
  };
}

export const DealsEventsView: React.FC<DealsEventsViewProps> = ({ onNavigateToStore, onAskConcierge, onAskAiPage }) => {
  const [sources, setSources] = useState<PublicKnowledgeSourceDto[]>([]);
  const [stores, setStores] = useState<StoreDto[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [submittedQuery, setSubmittedQuery] = useState('');
  const [selectedType, setSelectedType] = useState<OfferFilter>('All');
  const [loading, setLoading] = useState(true);
  const [agentLoading, setAgentLoading] = useState(false);
  const [agentResult, setAgentResult] = useState<StorefrontSearchResponse | null>(null);
  const [selectedPosterId, setSelectedPosterId] = useState<string | null>(null);
  const trackedSearchResultKey = useRef('');

  const recordBehavior = (input: ShopperBehaviorEventInput) => {
    void trackShopperBehavior(input).catch(() => undefined);
  };

  useEffect(() => {
    Promise.all([getPublishedKnowledgeSources('Deal'), getPublishedKnowledgeSources('Event'), getAllStores()])
      .then(([deals, events, storeData]) => { setSources([...deals, ...events]); setStores(storeData); })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    const query = submittedQuery.trim();
    if (query.length < 2) { setAgentResult(null); setAgentLoading(false); return; }
    setAgentResult(null);
    setAgentLoading(true);
    void searchStorefront(query, undefined, 'DealsEvents').then(setAgentResult).catch(() => setAgentResult(null)).finally(() => setAgentLoading(false));
  }, [submittedQuery]);

  const offers = useMemo(() => sources.map(parseOffer), [sources]);
  const storeForOffer = (offer: OfferDocument) => stores.find((candidate) => candidate.id === offer.tenantId)
    || stores.find((candidate) => candidate.name.toLowerCase() === (offer.tenant || offer.source.collectionName).toLowerCase());
  const filteredOffers = offers.filter((offer) => {
    if (selectedType !== 'All' && offer.source.sourceKind !== selectedType) return false;
    const query = submittedQuery.trim().toLowerCase();
    if (!query) return true;
    const knowledgeIds = new Set(agentResult?.knowledge.map((item) => item.sourceId) ?? []);
    const tenantIds = new Set(agentResult?.stores.map((item) => item.tenantId) ?? []);
    const store = storeForOffer(offer);
    return knowledgeIds.has(offer.source.id) || (store && tenantIds.has(store.id)) || [offer.source.title, offer.tenant, offer.source.collectionName, offer.searchableText]
      .filter(Boolean).join(' ').toLowerCase().includes(query);
  }).sort((left, right) => {
    if (!submittedQuery.trim() || !agentResult) return 0;
    const knowledgeRanks = new globalThis.Map(agentResult.knowledge.map((item, index) => [item.sourceId, index]));
    const tenantRanks = new globalThis.Map(agentResult.stores.map((item, index) => [item.tenantId, index]));
    const rank = (offer: OfferDocument) => knowledgeRanks.get(offer.source.id)
      ?? (storeForOffer(offer) ? 100 + (tenantRanks.get(storeForOffer(offer)!.id) ?? 1_000) : 1_000);
    return rank(left) - rank(right);
  });
  const hasAgentMatches = Boolean(agentResult && (agentResult.stores.length > 0 || agentResult.knowledge.length > 0));
  const posterOffers = filteredOffers.filter((offer) => offer.thumbnailUrl);
  const selectedPosterIndex = posterOffers.findIndex((offer) => offer.source.id === selectedPosterId);
  const selectedPoster = selectedPosterIndex >= 0 ? posterOffers[selectedPosterIndex] : null;
  const showPoster = (offset: number) => {
    if (!posterOffers.length) return;
    const nextIndex = (selectedPosterIndex + offset + posterOffers.length) % posterOffers.length;
    setSelectedPosterId(posterOffers[nextIndex].source.id);
  };

  useEffect(() => {
    const query = submittedQuery.trim();
    if (!agentResult || !query) return;
    const key = `${agentResult.sessionId}:${query}:${selectedType}`;
    if (trackedSearchResultKey.current === key) return;
    trackedSearchResultKey.current = key;
    recordBehavior({
      eventType: 'SearchResultsViewed',
      query,
      selectedFilter: selectedType,
      resultCount: filteredOffers.length,
      relatedTenantIds: filteredOffers.map(storeForOffer).filter((store): store is StoreDto => Boolean(store)).map((store) => store.id),
      relatedKnowledgeSourceIds: filteredOffers.map((offer) => offer.source.id),
    });
  }, [agentResult, filteredOffers, selectedType, submittedQuery]);

  useEffect(() => {
    if (!selectedPosterId || selectedPosterIndex < 0) return;
    const previousOverflow = document.body.style.overflow;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setSelectedPosterId(null);
      const offset = event.key === 'ArrowLeft' ? -1 : event.key === 'ArrowRight' ? 1 : 0;
      if (offset && posterOffers.length > 1) {
        const nextIndex = (selectedPosterIndex + offset + posterOffers.length) % posterOffers.length;
        setSelectedPosterId(posterOffers[nextIndex].source.id);
      }
    };
    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [selectedPosterId, selectedPosterIndex, posterOffers]);

  return <div className="space-y-8">
    <section className="grid gap-6 lg:grid-cols-[.8fr_1.2fr] lg:items-end">
      <div>
        <h2 className="text-3xl font-bold tracking-[-0.04em] md:text-4xl">Deals & events</h2>
        <p className="mt-2 text-sm leading-6 text-base-content/55">Offers and activities happening around the mall.</p>
      </div>
      <form className="surface-card flex min-h-16 items-center gap-3 px-5" onSubmit={(event) => { event.preventDefault(); setSubmittedQuery(searchQuery.trim()); }}>
        <Search size={19} className="shrink-0 text-base-content/45" />
        <input className="w-full bg-transparent text-sm outline-none placeholder:text-base-content/35" type="search" placeholder="Search deals, events or a budget" value={searchQuery} onChange={(event) => setSearchQuery(event.target.value)} />
        <button className="btn btn-primary btn-sm rounded-full px-5" disabled={agentLoading || searchQuery.trim().length < 2} type="submit">Search</button>
      </form>
    </section>

    <section className="flex flex-wrap items-center justify-between gap-3 border-y border-base-300/80 py-4">
      <div className="flex items-center gap-2">
        {(['All', 'Deal', 'Event'] as OfferFilter[]).map((type) => <button key={type} type="button" onClick={() => { setSelectedType(type); recordBehavior({ eventType: 'FilterChanged', query: submittedQuery || undefined, selectedFilter: type }); }} className={`rounded-full border px-4 py-2 text-xs font-semibold transition ${selectedType === type ? 'border-primary/40 bg-primary/5 text-primary' : 'border-transparent bg-base-100 text-base-content/55 hover:border-base-300'}`}>{type === 'All' ? 'All' : `${type}s`}</button>)}
      </div>
      <span className="text-xs text-base-content/45">{filteredOffers.length} {filteredOffers.length === 1 ? 'result' : 'results'}</span>
    </section>

    {agentLoading && <section className="surface-card p-5" aria-live="polite"><div className="flex items-start gap-3"><span className="relative mt-0.5 grid size-8 shrink-0 place-items-center rounded-full bg-primary/10 text-primary"><Sparkles size={16} /><span className="absolute inset-0 animate-ping rounded-full border border-primary/20" /></span><div className="min-w-0 flex-1"><div className="text-sm font-semibold">Searching…</div><div className="mt-3 space-y-2"><div className="h-2.5 w-3/4 animate-pulse rounded-full bg-base-300/70" /><div className="h-2.5 w-1/2 animate-pulse rounded-full bg-base-300/50" /></div></div></div></section>}

    {!agentLoading && agentResult && <section className="surface-card p-5" aria-live="polite"><div className="flex items-start gap-3"><span className="grid size-8 shrink-0 place-items-center rounded-full bg-primary/10 text-primary"><Sparkles size={16} /></span><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center justify-between gap-3"><h3 className="text-sm font-semibold">{hasAgentMatches ? 'Suggestions for you' : 'Ask the Sunway Kluang Mall AI'}</h3><button type="button" className="btn btn-ghost btn-sm rounded-full border border-base-300" onClick={() => { recordBehavior({ eventType: 'AskAiContinued', query: submittedQuery }); onAskAiPage(submittedQuery); }}><Bot size={14} />{hasAgentMatches ? 'Ask more' : 'Continue in Ask AI'}</button></div><AgentMarkdown>{hasAgentMatches ? agentResult.answer : 'This request is not specifically about a deal or event. Continue in Ask AI and your question will be carried over.'}</AgentMarkdown></div></div></section>}

    {loading ? <div className="py-20 text-center text-base-content/45"><span className="loading loading-spinner loading-md" /><p className="mt-3">Loading deals and events…</p></div>
      : !agentLoading && filteredOffers.length === 0 && !agentResult ? <div className="surface-card py-16 text-center text-sm text-base-content/50">No deals or events matched your filters.</div>
        : !agentLoading && filteredOffers.length === 0 ? null
        : <section className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">{filteredOffers.map((offer) => {
          const store = storeForOffer(offer);
          const owner = offer.tenant || store?.name || (offer.source.collectionName.toLowerCase().includes('mall') ? 'Mall-wide' : offer.source.collectionName);
          return <article key={offer.source.id} className="surface-card flex min-h-72 flex-col overflow-hidden">
            <div className="aspect-[16/9] overflow-hidden bg-base-200/70">
              {offer.thumbnailUrl
                ? <button type="button" className="group relative block h-full w-full cursor-zoom-in" onClick={() => { setSelectedPosterId(offer.source.id); recordBehavior({ eventType: 'ContentOpened', query: submittedQuery || undefined, selectedFilter: selectedType, targetKnowledgeSourceId: offer.source.id, targetTenantId: store?.id, targetTitle: offer.source.title }); }} aria-label={`Open poster gallery at ${offer.source.title}`}>
                  <img src={offer.thumbnailUrl} alt={`${offer.source.title} poster`} className="h-full w-full object-cover transition duration-300 group-hover:scale-[1.03]" loading="lazy" />
                  <span className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/65 to-transparent px-4 pb-3 pt-10 text-left text-xs font-semibold text-white opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">View poster</span>
                </button>
                : <div className="grid h-full place-items-center text-base-content/20"><ImageIcon size={30} strokeWidth={1.4} /></div>}
            </div>
            <div className="flex flex-1 flex-col p-5">
              <div className="flex items-center justify-between gap-3 text-xs text-base-content/50">
                <span className="font-semibold">{offer.source.sourceKind}</span>
                {store && <span className="inline-flex items-center gap-1"><MapPin size={12} />{store.unitNumber} · Level {store.floorNumber}</span>}
              </div>
              <h3 className="mt-3 text-lg font-bold leading-snug tracking-tight">{offer.source.title}</h3>
              <p className="mt-2 text-sm font-medium text-base-content/55">{owner}</p>
              <div className="mt-auto flex gap-2 border-t border-base-300/70 pt-4">
                {store && <button onClick={() => { recordBehavior({ eventType: 'DirectionsRequested', query: submittedQuery || undefined, selectedFilter: selectedType, targetKnowledgeSourceId: offer.source.id, targetTenantId: store.id, targetTitle: offer.source.title }); onNavigateToStore(store); }} className="btn btn-primary btn-sm flex-1 rounded-full"><Navigation size={13} /> Directions</button>}
                <button onClick={() => { recordBehavior({ eventType: 'AskAiContinued', query: submittedQuery || undefined, selectedFilter: selectedType, targetKnowledgeSourceId: offer.source.id, targetTenantId: store?.id, targetTitle: offer.source.title }); onAskConcierge(`Tell me more about "${offer.source.title}" at ${owner}`); }} className={`btn btn-ghost btn-sm rounded-full border border-base-300 ${store ? '' : 'flex-1'}`}><Sparkles size={13} /> Ask</button>
              </div>
            </div>
          </article>;
        })}</section>}

    {selectedPoster && <div className="fixed inset-0 z-[100] flex flex-col bg-black/90 p-3 backdrop-blur-sm md:p-6" role="dialog" aria-modal="true" aria-label="Poster gallery" onMouseDown={(event) => { if (event.target === event.currentTarget) setSelectedPosterId(null); }}>
      <div className="flex items-center justify-between gap-4 text-white">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold">{selectedPoster.source.title}</p>
          <p className="text-xs text-white/60">Poster {selectedPosterIndex + 1} of {posterOffers.length}</p>
        </div>
        <button type="button" className="btn btn-circle btn-ghost text-white hover:bg-white/15" onClick={() => setSelectedPosterId(null)} aria-label="Close poster gallery"><X size={22} /></button>
      </div>
      <div className="relative flex min-h-0 flex-1 items-center justify-center py-4" onMouseDown={(event) => { if (event.target === event.currentTarget) setSelectedPosterId(null); }}>
        {posterOffers.length > 1 && <button type="button" className="btn btn-circle absolute left-0 z-10 border-0 bg-black/45 text-white hover:bg-black/70 md:left-3" onClick={() => showPoster(-1)} aria-label="Previous poster"><ChevronLeft size={25} /></button>}
        <img src={selectedPoster.thumbnailUrl} alt={`${selectedPoster.source.title} poster`} className="max-h-full max-w-full rounded-lg object-contain shadow-2xl" />
        {posterOffers.length > 1 && <button type="button" className="btn btn-circle absolute right-0 z-10 border-0 bg-black/45 text-white hover:bg-black/70 md:right-3" onClick={() => showPoster(1)} aria-label="Next poster"><ChevronRight size={25} /></button>}
      </div>
      {posterOffers.length > 1 && <div className="mx-auto flex max-w-full gap-2 overflow-x-auto pb-1" aria-label="Choose a poster">
        {posterOffers.map((poster, index) => <button type="button" key={poster.source.id} className={`h-14 w-20 shrink-0 overflow-hidden rounded-md border-2 transition ${poster.source.id === selectedPoster.source.id ? 'border-white opacity-100' : 'border-transparent opacity-55 hover:opacity-90'}`} onClick={() => setSelectedPosterId(poster.source.id)} aria-label={`View poster ${index + 1}: ${poster.source.title}`} aria-current={poster.source.id === selectedPoster.source.id ? 'true' : undefined}><img src={poster.thumbnailUrl} alt="" className="h-full w-full object-cover" /></button>)}
      </div>}
    </div>}
  </div>;
};
