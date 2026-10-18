import { ArrowRight, Compass, Search, Sparkles, Tag } from 'lucide-react';
import { mallConfig } from '../mallConfig';

interface HomeViewProps {
  onDirectory: () => void;
  onOffers: () => void;
  onDirections: () => void;
  onAsk: (query?: string) => void;
}

export function HomeView({ onDirectory, onOffers, onDirections, onAsk }: HomeViewProps) {
  return <div className="space-y-10">
    <section className="mall-hero overflow-hidden rounded-3xl border border-base-300 px-6 py-12 md:px-12 md:py-16">
      <div className="relative z-10 max-w-3xl">
        <span className="inline-flex items-center gap-2 rounded-full bg-primary/10 px-3 py-1 text-xs font-bold text-primary"><Sparkles size={13} /> {mallConfig.name}</span>
        <h1 className="mt-5 text-4xl font-bold leading-[1.04] tracking-[-0.05em] md:text-6xl">What would you like to do <span className="text-primary">today?</span></h1>
        <p className="mt-5 max-w-xl text-base leading-7 text-base-content/60 md:text-lg">Find a store, discover current Sunway Kluang deals and events, or ask the mall guide for a recommendation.</p>
        <div className="mt-8 flex flex-wrap gap-3"><button className="btn btn-primary rounded-full px-6" onClick={() => onAsk()}><Sparkles size={16} /> Ask the mall guide</button><button className="btn rounded-full border-base-300 bg-base-100 px-6" onClick={onDirectory}><Search size={16} /> Browse directory</button></div>
      </div>
    </section>

    <section>
      <div className="mb-5"><h2 className="text-2xl font-bold tracking-tight">Plan your visit</h2><p className="mt-1 text-sm text-base-content/50">Start with one clear task.</p></div>
      <div className="grid gap-5 md:grid-cols-3">
        <button className="surface-card interactive-card p-6 text-left" onClick={onDirectory}><span className="grid size-11 place-items-center rounded-xl bg-primary/10 text-primary"><Search size={20} /></span><h3 className="mt-5 text-lg font-bold">Find stores</h3><p className="mt-2 min-h-12 text-sm leading-6 text-base-content/55">Search by store, product, service, or facility.</p><span className="mt-5 inline-flex items-center gap-1 text-sm font-bold text-primary">Open directory <ArrowRight size={14} /></span></button>
        <button className="surface-card interactive-card p-6 text-left" onClick={onOffers}><span className="grid size-11 place-items-center rounded-xl bg-secondary/10 text-secondary"><Tag size={20} /></span><h3 className="mt-5 text-lg font-bold">Deals & events</h3><p className="mt-2 min-h-12 text-sm leading-6 text-base-content/55">See promotions and activities happening now.</p><span className="mt-5 inline-flex items-center gap-1 text-sm font-bold text-secondary">See what’s on <ArrowRight size={14} /></span></button>
        <button className="surface-card interactive-card p-6 text-left" onClick={onDirections}><span className="grid size-11 place-items-center rounded-xl bg-accent/10 text-accent"><Compass size={20} /></span><h3 className="mt-5 text-lg font-bold">Get directions</h3><p className="mt-2 min-h-12 text-sm leading-6 text-base-content/55">Receive simple, landmark-based walking instructions.</p><span className="mt-5 inline-flex items-center gap-1 text-sm font-bold">Plan a route <ArrowRight size={14} /></span></button>
      </div>
    </section>

    <section className="rounded-3xl bg-neutral px-6 py-8 text-neutral-content md:flex md:items-center md:justify-between md:px-10">
      <div><h2 className="text-2xl font-bold">Not sure where to start?</h2><p className="mt-2 text-sm opacity-65">Describe what you need in everyday language.</p></div>
      <div className="mt-5 flex flex-wrap gap-2 md:mt-0">{['Where can I eat?', 'Show me current deals'].map((query) => <button key={query} className="btn btn-sm rounded-full border-white/15 bg-white/10 text-white hover:bg-white/20" onClick={() => onAsk(query)}>{query}</button>)}</div>
    </section>
  </div>;
}
