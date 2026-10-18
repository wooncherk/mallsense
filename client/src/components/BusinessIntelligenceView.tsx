import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, BarChart3, Bot, Eye, History, MessageSquarePlus, MessageSquareText, MoreHorizontal, Navigation, RefreshCw, Search, Send, Sparkles, UsersRound, X } from 'lucide-react';
import { askTodayBi, generateTodayBiNarrative, getBiChat, getBiChatHistory, getBiSummaryHistory, getLatestBiSummary, getTodayBiSummary } from '../api';
import type { BiChatTurnDto, BiConversationSummaryDto, BiNarrativeDto, BiSummaryHistoryDto, DailyBiSummaryDto, UserProfileDto } from '../types';

interface BusinessIntelligenceViewProps { profile: UserProfileDto; }
type ReportingDays = 1 | 7 | 30;

const rangeLabels: Record<ReportingDays, string> = { 1: 'Today', 7: 'Last 7 days', 30: 'Last 30 days' };
const suggestedQuestions = ['What needs attention?', 'Where are shoppers dropping off?', 'What should the team do next?'];

export function BusinessIntelligenceView({ profile }: BusinessIntelligenceViewProps) {
  const [summary, setSummary] = useState<DailyBiSummaryDto | null>(null);
  const [narrative, setNarrative] = useState<BiNarrativeDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState('');
  const [chatOpen, setChatOpen] = useState(false);
  const [chatBusy, setChatBusy] = useState(false);
  const [chatInput, setChatInput] = useState('');
  const [chatError, setChatError] = useState('');
  const [messages, setMessages] = useState<BiChatTurnDto[]>([]);
  const [chatSessionId, setChatSessionId] = useState<string>();
  const [chatHistoryOpen, setChatHistoryOpen] = useState(false);
  const [chatHistoryBusy, setChatHistoryBusy] = useState(false);
  const [chatHistory, setChatHistory] = useState<BiConversationSummaryDto[]>([]);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyBusy, setHistoryBusy] = useState(false);
  const [history, setHistory] = useState<BiSummaryHistoryDto[]>([]);
  const [days, setDays] = useState<ReportingDays>(1);

  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const [liveSummary, latest] = await Promise.all([getTodayBiSummary(days), getLatestBiSummary(days)]);
      setSummary(liveSummary);
      setNarrative(latest ? {
        id: latest.id, scope: latest.scope, scopeName: latest.scopeName, localDate: latest.periodLabel,
        summaryText: latest.summaryText, generatedAtUtc: latest.generatedAtUtc, trigger: latest.trigger,
      } : null);
    }
    catch (loadError) { setError(loadError instanceof Error ? loadError.message : 'Unable to load business intelligence.'); }
    finally { setLoading(false); }
  }, [days]);

  const generateNarrative = useCallback(async (trigger: 'Manual' | 'Scheduled' = 'Manual') => {
    setGenerating(true); setError('');
    try {
      const result = await generateTodayBiNarrative(days, trigger);
      setNarrative(result);
    } catch (generateError) {
      setError(generateError instanceof Error ? generateError.message : 'Unable to generate the AI summary.');
    } finally { setGenerating(false); }
  }, [days]);

  const openHistory = async () => {
    setHistoryOpen(true); setHistoryBusy(true); setError('');
    try { setHistory(await getBiSummaryHistory(days)); }
    catch (historyError) { setError(historyError instanceof Error ? historyError.message : 'Unable to load summary history.'); }
    finally { setHistoryBusy(false); }
  };

  const sendQuestion = async (question = chatInput) => {
    const cleanQuestion = question.trim();
    if (!cleanQuestion || chatBusy) return;
    const history = messages.slice(-8);
    setMessages((current) => [...current, { role: 'user', message: cleanQuestion }]);
    setChatInput(''); setChatError(''); setChatBusy(true);
    try {
      const response = await askTodayBi(cleanQuestion, history, days, chatSessionId);
      setChatSessionId(response.sessionId);
      setMessages((current) => [...current, { role: 'assistant', message: response.answer }]);
    } catch (chatFailure) {
      setChatError(chatFailure instanceof Error ? chatFailure.message : 'Unable to reach the Sunway Kluang Mall AI.');
    } finally { setChatBusy(false); }
  };

  const startNewChat = () => {
    setChatSessionId(undefined);
    setMessages([]);
    setChatInput('');
    setChatError('');
    setChatHistoryOpen(false);
  };

  const openChatHistory = async () => {
    setChatHistoryOpen(true);
    setChatHistoryBusy(true);
    setChatError('');
    try { setChatHistory(await getBiChatHistory(days)); }
    catch (historyError) { setChatError(historyError instanceof Error ? historyError.message : 'Unable to load AI conversation history.'); }
    finally { setChatHistoryBusy(false); }
  };

  const resumeChat = async (sessionId: string) => {
    setChatHistoryBusy(true);
    setChatError('');
    try {
      const conversation = await getBiChat(sessionId);
      setChatSessionId(conversation.sessionId);
      setMessages(conversation.turns.map((turn) => ({ role: turn.role, message: turn.message })));
      setChatHistoryOpen(false);
    } catch (historyError) {
      setChatError(historyError instanceof Error ? historyError.message : 'Unable to open the AI conversation.');
    } finally { setChatHistoryBusy(false); }
  };

  useEffect(() => { void load(); }, [load, profile.id]);
  useEffect(() => {
    const timer = window.setInterval(() => { void load(); }, 60_000);
    return () => window.clearInterval(timer);
  }, [load]);

  if (loading && !summary) return <div className="grid min-h-96 place-items-center"><span className="loading loading-spinner loading-md text-primary" /></div>;

  return <div className="mx-auto max-w-7xl space-y-6">
    <section className="admin-page-header flex flex-wrap items-center justify-between gap-5 p-6 md:p-8">
      <div><h2 className="text-3xl font-bold tracking-[-0.04em]">{profile.role === 'Admin' ? 'Mall business intelligence' : `${profile.tenantName} intelligence`}</h2><p className="mt-2 max-w-2xl text-sm leading-6 text-base-content/55">Understand shopper behaviour across the selected period and question the evidence with AI.</p></div>
      <div className="grid w-full gap-2 sm:w-auto sm:grid-cols-[minmax(12rem,1fr)_auto]"><select className="select select-bordered h-11 w-full rounded-xl border-base-300 bg-base-100" value={days} onChange={(event) => { setDays(Number(event.target.value) as ReportingDays); setNarrative(null); startNewChat(); }} aria-label="Reporting period"><option value={1}>Today</option><option value={7}>Last 7 days</option><option value={30}>Last 30 days</option></select><button type="button" className="btn h-11 rounded-xl border-base-300 bg-base-100 px-5 shadow-none hover:border-primary/35 hover:bg-primary/5 hover:text-primary" onClick={() => setChatOpen(true)}><MessageSquareText size={16} /> Ask AI</button></div>
    </section>

    {error && <div className="alert alert-error text-sm"><AlertTriangle size={17} />{error}</div>}
    {summary && <>
      <section className="surface-card overflow-hidden">
        <div className="flex flex-wrap items-start justify-between gap-4 border-b border-base-300 p-5 md:px-6"><div className="flex gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary"><Sparkles size={19} /></span><div><h3 className="font-bold">Latest AI summary</h3><p className="mt-1 text-xs text-base-content/45">The latest saved summary message for {rangeLabels[days].toLowerCase()}.</p></div></div><div className="flex items-center gap-2">{narrative && <span className="badge badge-success badge-soft">Summary</span>}<details className="dropdown dropdown-end"><summary className="btn btn-ghost btn-sm btn-circle list-none" aria-label="Summary options"><MoreHorizontal size={18} /></summary><div className="dropdown-content z-30 mt-2 w-48 rounded-xl border border-base-300 bg-base-100 p-1.5 shadow-xl"><button className="w-full rounded-lg px-3 py-2 text-left text-sm font-semibold hover:bg-base-200" onClick={(event) => { event.currentTarget.closest('details')?.removeAttribute('open'); void openHistory(); }}>View summary history</button></div></details></div></div>
        <div className="p-5 md:p-6">{narrative ? <><p className="whitespace-pre-wrap text-[15px] leading-7 text-base-content/75">{narrative.summaryText}</p><div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-base-300 pt-4 text-xs text-base-content/45"><span>{narrative.scopeName} · {narrative.localDate} · {narrative.trigger}</span><button className="btn btn-ghost btn-sm" disabled={generating} onClick={() => void generateNarrative()}><RefreshCw size={13} /> Generate latest</button></div></> : <div className="rounded-2xl border border-dashed border-primary/25 bg-primary/[.035] p-6 text-center"><Bot className="mx-auto text-primary" size={28} /><h4 className="mt-3 font-bold">No saved summary for this period yet</h4><p className="mx-auto mt-2 max-w-xl text-sm leading-6 text-base-content/55">Generate a concise text summary of shopper activity, conversion signals, gaps, and the next item that deserves human attention.</p><button className="btn btn-primary btn-sm mt-5" disabled={generating} onClick={() => void generateNarrative()}>{generating ? <span className="loading loading-spinner loading-sm" /> : <Sparkles size={14} />} Generate now</button></div>}</div>
      </section>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"><Metric label="Searches" value={summary.totalSearches} detail={`${summary.uniqueSessions} unique sessions`} icon={Search} /><Metric label="Offer opens" value={summary.contentOpens} detail={`${summary.engagementRate}% session engagement`} icon={Eye} /><Metric label="Ask AI" value={summary.askAiContinuations} detail="Continued discovery" icon={MessageSquareText} /><Metric label="Directions" value={summary.directionRequests} detail="High visit intent" icon={Navigation} /></div>
      <div className="grid gap-5 xl:grid-cols-[1.15fr_.85fr]">
        <section className="surface-card p-5 md:p-6"><div className="flex items-center justify-between gap-4"><div><h3 className="font-bold">Recorded activity</h3><p className="mt-1 text-xs text-base-content/45">Verified signals behind the AI summary.</p></div><span className="badge badge-ghost">{rangeLabels[days]}</span></div><div className="mt-5 space-y-3">{summary.highlights.map((highlight) => <div key={highlight} className="flex gap-3 rounded-xl bg-base-200/65 p-4 text-sm leading-6"><BarChart3 className="mt-0.5 shrink-0 text-primary" size={16} /><span>{highlight}</span></div>)}</div></section>
        <section className="surface-card p-5 md:p-6"><h3 className="font-bold">Search health</h3><div className="mt-5 grid grid-cols-2 gap-3"><SmallMetric label="Deal searches" value={summary.dealSearches} /><SmallMetric label="Event searches" value={summary.eventSearches} /><SmallMetric label="Zero results" value={summary.zeroResultSearches} warning={summary.zeroResultSearches > 0} /><SmallMetric label="Engagement" value={`${summary.engagementRate}%`} /></div></section>
      </div>

      <div className="grid gap-5 lg:grid-cols-2"><Ranking title="Top searches" empty="No searches recorded yet." items={summary.topQueries} /><Ranking title="Most engaged content" empty="No content engagement recorded yet." items={summary.topContent} /></div>
      <div className="flex flex-wrap items-center justify-between gap-3 text-xs text-base-content/40"><span className="inline-flex items-center gap-1.5"><UsersRound size={13} />{summary.scopeName} scope · anonymous visit sessions</span><span>Live data updated {new Date(summary.generatedAtUtc).toLocaleTimeString()}</span></div>
    </>}

    {historyOpen && <div className="fixed inset-0 z-[85] grid place-items-center bg-neutral/40 p-3 backdrop-blur-sm" role="dialog" aria-modal="true" aria-labelledby="summary-history-title" onMouseDown={(event) => { if (event.target === event.currentTarget) setHistoryOpen(false); }}><section className="flex max-h-[88vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-base-300 bg-base-100 shadow-2xl"><div className="flex items-start justify-between gap-4 border-b border-base-300 p-5"><div><h2 id="summary-history-title" className="text-lg font-bold">Summary history</h2><p className="mt-1 text-xs text-base-content/45">{rangeLabels[days]} · saved AI conversation messages marked as summaries</p></div><button className="btn btn-ghost btn-sm btn-circle" onClick={() => setHistoryOpen(false)} aria-label="Close summary history"><X size={18} /></button></div><div className="flex-1 overflow-y-auto p-5">{historyBusy && <div className="grid min-h-48 place-items-center"><span className="loading loading-spinner loading-md text-primary" /></div>}{!historyBusy && history.length === 0 && <div className="grid min-h-48 place-items-center text-center"><div><MessageSquareText className="mx-auto text-base-content/25" size={32} /><p className="mt-3 font-semibold">No saved summaries yet</p><p className="mt-1 text-sm text-base-content/45">Generate a summary to start this history.</p></div></div>}{!historyBusy && history.length > 0 && <div className="space-y-4">{history.map((item, index) => <article key={item.id} className="rounded-2xl border border-base-300 bg-base-200/45 p-5"><div className="flex flex-wrap items-center justify-between gap-3"><div className="flex items-center gap-2"><span className="badge badge-primary badge-soft">Summary</span>{index === 0 && <span className="badge badge-success badge-soft">Latest</span>}<span className="badge badge-ghost">{item.trigger}</span></div><time className="text-xs text-base-content/40">{new Date(item.generatedAtUtc).toLocaleString()}</time></div><p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-base-content/70">{item.summaryText}</p><div className="mt-4 border-t border-base-300 pt-3 text-xs text-base-content/40">{item.scopeName} · {item.periodLabel}</div></article>)}</div>}</div></section></div>}

    {chatOpen && <div className="fixed inset-0 z-[80] flex justify-end bg-neutral/35 backdrop-blur-[2px]" role="dialog" aria-modal="true" aria-labelledby="bi-chat-title" onMouseDown={(event) => { if (event.target === event.currentTarget) setChatOpen(false); }}><aside className="flex h-full w-full max-w-lg flex-col border-l border-base-300 bg-base-100 shadow-2xl">
      <div className="flex items-start justify-between gap-4 border-b border-base-300 p-5"><div className="flex min-w-0 gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary text-primary-content"><Bot size={20} /></span><div className="min-w-0"><h2 id="bi-chat-title" className="truncate font-bold">{chatHistoryOpen ? 'Conversation history' : 'Ask intelligence'}</h2><p className="mt-1 truncate text-xs text-base-content/45">{rangeLabels[days]} · grounded in {summary?.scopeName || 'your'} BI signals</p></div></div><div className="flex shrink-0 items-center"><button type="button" className={`btn btn-ghost btn-sm btn-circle ${chatHistoryOpen ? 'bg-primary/10 text-primary' : ''}`} onClick={() => chatHistoryOpen ? setChatHistoryOpen(false) : void openChatHistory()} aria-label={chatHistoryOpen ? 'Back to conversation' : 'Conversation history'}><History size={17} /></button><button type="button" className="btn btn-ghost btn-sm btn-circle" onClick={startNewChat} aria-label="New conversation"><MessageSquarePlus size={17} /></button><button type="button" className="btn btn-ghost btn-sm btn-circle" onClick={() => setChatOpen(false)} aria-label="Close AI chat"><X size={18} /></button></div></div>
      {chatHistoryOpen ? <div className="flex-1 overflow-y-auto p-5">{chatHistoryBusy && <div className="grid min-h-48 place-items-center"><span className="loading loading-spinner loading-md text-primary" /></div>}{!chatHistoryBusy && chatHistory.length === 0 && <div className="grid min-h-64 place-items-center text-center"><div><History className="mx-auto text-base-content/25" size={34} /><p className="mt-3 font-semibold">No saved conversations yet</p><p className="mt-1 text-sm text-base-content/45">Your Ask AI chats will appear here after the first reply.</p><button type="button" className="btn btn-primary btn-sm mt-4" onClick={startNewChat}><MessageSquarePlus size={15} /> Start a chat</button></div></div>}{!chatHistoryBusy && chatHistory.length > 0 && <div className="space-y-2">{chatHistory.map((conversation) => <button type="button" key={conversation.sessionId} className={`block w-full rounded-xl border p-4 text-left transition hover:border-primary/35 hover:bg-primary/5 ${conversation.sessionId === chatSessionId ? 'border-primary/35 bg-primary/5' : 'border-base-300'}`} onClick={() => void resumeChat(conversation.sessionId)}><div className="truncate font-bold">{conversation.title}</div><p className="mt-1 line-clamp-2 text-sm text-base-content/55">{conversation.lastMessage}</p><div className="mt-3 text-xs text-base-content/40">{new Date(conversation.updatedAtUtc).toLocaleString()} · {Math.ceil(conversation.turnCount / 2)} {conversation.turnCount <= 2 ? 'exchange' : 'exchanges'}</div></button>)}</div>}{chatError && <div className="alert alert-error mt-4 text-sm"><AlertTriangle size={15} />{chatError}</div>}</div> : <>
      <div className="flex-1 overflow-y-auto p-5">{messages.length === 0 && <div className="rounded-2xl bg-base-200/60 p-5"><div className="text-sm font-bold">What would you like to understand?</div><p className="mt-2 text-sm leading-6 text-base-content/55">I can explain demand, engagement, search gaps, and approval-gated next steps across the selected period. I won’t invent data or take action.</p><div className="mt-4 flex flex-wrap gap-2">{suggestedQuestions.map((question) => <button key={question} className="btn btn-ghost btn-sm h-auto min-h-9 rounded-full border border-base-300 bg-base-100 py-2 text-left" onClick={() => void sendQuestion(question)}>{question}</button>)}</div></div>}<div className="space-y-4">{messages.map((message, index) => <div key={`${message.role}-${index}`} className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}><div className={`max-w-[88%] whitespace-pre-wrap rounded-2xl px-4 py-3 text-sm leading-6 ${message.role === 'user' ? 'bg-primary text-primary-content' : 'border border-base-300 bg-base-200/65'}`}>{message.message}</div></div>)}</div>{chatBusy && <div className="mt-4 flex justify-start"><div className="rounded-2xl border border-base-300 bg-base-200/65 px-4 py-3"><span className="loading loading-dots loading-sm" /></div></div>}{chatError && <div className="alert alert-error mt-4 text-sm"><AlertTriangle size={15} />{chatError}</div>}</div>
      <form className="border-t border-base-300 p-4" onSubmit={(event) => { event.preventDefault(); void sendQuestion(); }}><div className="flex gap-2"><textarea className="textarea textarea-bordered min-h-12 flex-1 resize-none" rows={1} value={chatInput} onChange={(event) => setChatInput(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void sendQuestion(); } }} placeholder="Ask about this period…" /><button className="btn btn-primary btn-square" disabled={!chatInput.trim() || chatBusy} aria-label="Send question"><Send size={17} /></button></div><p className="mt-2 text-[11px] text-base-content/40">This conversation is saved after every AI reply. Operational changes still require approval.</p></form></>}
    </aside></div>}
  </div>;
}

function Metric({ label, value, detail, icon: Icon }: { label: string; value: number | string; detail: string; icon: typeof Search }) { return <div className="surface-card p-5"><div className="flex items-center justify-between"><span className="text-xs font-bold uppercase tracking-wider text-base-content/40">{label}</span><Icon className="text-primary" size={18} /></div><div className="mt-3 text-3xl font-bold">{value}</div><div className="mt-2 text-xs text-base-content/45">{detail}</div></div>; }
function SmallMetric({ label, value, warning = false }: { label: string; value: number | string; warning?: boolean }) { return <div className={`rounded-xl border p-4 ${warning ? 'border-error/25 bg-error/5' : 'border-base-300 bg-base-200/45'}`}><div className="text-xs text-base-content/50">{label}</div><div className={`mt-2 text-2xl font-bold ${warning ? 'text-error' : ''}`}>{value}</div></div>; }
function Ranking({ title, empty, items }: { title: string; empty: string; items: Array<{ label: string; count: number }> }) { const maximum = Math.max(...items.map((item) => item.count), 1); return <section className="surface-card p-5 md:p-6"><h3 className="font-bold">{title}</h3>{items.length === 0 ? <p className="mt-6 text-sm text-base-content/45">{empty}</p> : <div className="mt-5 space-y-4">{items.map((item) => <div key={item.label}><div className="flex items-center justify-between gap-4 text-sm"><span className="truncate font-medium">{item.label}</span><span className="font-bold">{item.count}</span></div><div className="mt-2 h-1.5 overflow-hidden rounded-full bg-base-300"><div className="h-full rounded-full bg-primary" style={{ width: `${item.count / maximum * 100}%` }} /></div></div>)}</div>}</section>; }
