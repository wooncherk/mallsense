import React, { useState, useEffect, useRef } from 'react';
import {
  Send,
  Navigation,
  Compass,
  Sparkles,
  MapPin,
  Tag,
  Accessibility,
  ArrowRight,
  EllipsisVertical,
  History,
  MessageSquarePlus,
  Trash2,
  X,
  Paperclip,
  FileText,
} from 'lucide-react';
import {
  streamShopperAi,
  getLatestShopperConversation,
  getShopperConversationHistory,
  getShopperConversation,
  deleteShopperConversation,
  getRoute,
  getRouteOverview,
  getNavigationLocations,
} from '../api';
import type {
  ShopperChatResponse,
  StructuredIntentDto,
  RecommendationCardDto,
  RouteResponse,
  NavigationLocation,
  ShopperReplySuggestionDto,
  ShopperConversationHistoryDto,
  ShopperConversationSummaryDto,
} from '../types';
import { AgentMarkdown } from './AgentMarkdown';

interface Message {
  id: string;
  sender: 'user' | 'assistant';
  text: string;
  structuredIntent?: StructuredIntentDto;
  recommendations?: RecommendationCardDto[];
  activity?: string[];
  isStreaming?: boolean;
  replySuggestions?: ShopperReplySuggestionDto[];
  timestamp: string;
  attachments?: ChatAttachment[];
}

interface ChatAttachment {
  id: string;
  name: string;
  type: string;
  size: number;
  previewUrl?: string;
  textContent?: string;
  base64Data?: string;
}

const MAX_ATTACHMENTS = 4;
const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;
const TEXT_ATTACHMENT_TYPES = new Set([
  'application/json',
  'text/csv',
  'text/markdown',
  'text/plain',
]);

interface ShopperViewProps {
  initialQuery?: string;
  initialDestinationId?: string;
  initialTab?: 'chat' | 'navigate';
  chatOnly?: boolean;
  navigationOnly?: boolean;
  onNavigateToNode?: (nodeId: string, destinationName: string) => void;
  onOpenDirectory?: () => void;
  onOpenDeals?: () => void;
  onOpenDirections?: () => void;
  userId?: string;
}

const createWelcomeMessage = (): Message => ({
  id: 'welcome',
  sender: 'assistant',
  text: "Welcome to the Sunway Kluang Mall digital concierge. I can help you explore stores, dining, current deals and mall events. How may I assist your visit today?",
  timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
});

const historyToMessages = (history: ShopperConversationHistoryDto): Message[] => history.turns.map((turn) => ({
  id: turn.id,
  sender: turn.role,
  text: turn.message,
  structuredIntent: turn.structuredIntent ?? undefined,
  recommendations: turn.recommendations,
  replySuggestions: turn.replySuggestions,
  timestamp: new Date(turn.createdAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
}));

export const ShopperView: React.FC<ShopperViewProps> = ({
  initialQuery,
  initialDestinationId,
  initialTab = 'chat',
  chatOnly = false,
  navigationOnly = false,
  onNavigateToNode,
  userId,
}) => {
  const [activeTab, setActiveTab] = useState<'chat' | 'navigate'>(initialTab);
  const [messages, setMessages] = useState<Message[]>(() => [createWelcomeMessage()]);
  const [inputQuery, setInputQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [sessionId, setSessionId] = useState<string>('');
  const [currentIntent, setCurrentIntent] = useState<StructuredIntentDto | null>(null);
  const [historyLoaded, setHistoryLoaded] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [conversationHistory, setConversationHistory] = useState<ShopperConversationSummaryDto[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState('');
  const [pendingDeleteSessionId, setPendingDeleteSessionId] = useState<string | null>(null);
  const [attachments, setAttachments] = useState<ChatAttachment[]>([]);
  const [attachmentError, setAttachmentError] = useState('');
  const lastInitialQuery = useRef<string | undefined>(undefined);
  const chatStreamRef = useRef<HTMLDivElement>(null);
  const chatInputRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Navigation states
  const [nodes, setNodes] = useState<NavigationLocation[]>([]);
  const [startNodeId, setStartNodeId] = useState<string>('');
  const [destinationNodeId, setDestinationNodeId] = useState<string>('');
  const [accessibleOnly, setAccessibleOnly] = useState(false);
  const [route, setRoute] = useState<RouteResponse | null>(null);
  const [navigatingTo, setNavigatingTo] = useState<string>('');
  const [navLoading, setNavLoading] = useState(false);
  const [overviewLoading, setOverviewLoading] = useState(false);
  const navRequestIdRef = useRef(0);

  useEffect(() => {
    if (!userId) return;
    let cancelled = false;
    setHistoryLoaded(false);
    getLatestShopperConversation()
      .then((history) => {
        if (cancelled || !history) return;
        setSessionId(history.sessionId);
        setCurrentIntent(history.latestIntent);
        setMessages(historyToMessages(history));
      })
      .catch(() => undefined)
      .finally(() => { if (!cancelled) setHistoryLoaded(true); });
    return () => { cancelled = true; };
  }, [userId]);

  useEffect(() => {
    getNavigationLocations().then((data) => {
      setNodes(data);
      const entA = data.find((n) => n.label.includes('Entrance A'));
      if (entA) setStartNodeId(entA.id);

      if (initialDestinationId) {
        setDestinationNodeId(initialDestinationId);
        const dest = data.find((n) => n.id === initialDestinationId);
        if (dest) setNavigatingTo(dest.label);
        if (entA) {
          calculatePath(entA.id, initialDestinationId, accessibleOnly);
        }
      } else {
        const popular = data.find((n) => n.label.includes('Popular'));
        if (popular) setDestinationNodeId(popular.id);
      }
    });
  }, [initialDestinationId]);

  useEffect(() => {
    if (initialTab) {
      setActiveTab(initialTab);
    }
  }, [initialTab]);

  useEffect(() => {
    if (historyLoaded && initialQuery && initialQuery !== lastInitialQuery.current) {
      lastInitialQuery.current = initialQuery;
      handleSend(initialQuery);
    }
  }, [historyLoaded, initialQuery]);

  useEffect(() => {
    if (chatOnly) {
      window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'smooth' });
      return;
    }
    chatStreamRef.current?.scrollTo({ top: chatStreamRef.current.scrollHeight, behavior: 'smooth' });
  }, [chatOnly, messages]);

  const handleSend = async (displayText?: string, expandedPrompt?: string) => {
    const text = (displayText || inputQuery).trim();
    const displayTextForConversation = text || 'Please review the attached file.';
    const attachmentContext = attachments.map((attachment) => {
      if (attachment.textContent) {
        return `Attached file: ${attachment.name}\n${attachment.textContent}`;
      }
      return `Attached ${attachment.type.startsWith('image/') ? 'image' : 'file'}: ${attachment.name} (${attachment.type || 'unknown type'}).`;
    }).join('\n\n');
    const prompt = [expandedPrompt || text, attachmentContext].filter(Boolean).join('\n\n').trim();
    if ((!text && attachments.length === 0) || !prompt || loading || !userId || !historyLoaded) return;

    const userMsg: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text: displayTextForConversation,
      attachments,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };

    const assistantId = `${Date.now()}-assistant`;
    const conversation = messages.slice(-8).map((message) => ({
      role: message.sender,
      message: message.text,
    }));

    setMessages((prev) => [...prev, userMsg, {
      id: assistantId,
      sender: 'assistant',
      text: '',
      activity: [],
      isStreaming: true,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    }]);
    setInputQuery('');
    setAttachments([]);
    setAttachmentError('');
    setLoading(true);

    try {
      await streamShopperAi(
        prompt,
        currentIntent,
        sessionId || undefined,
        conversation,
        {
          onStatus: (status) => setMessages((current) => current.map((message) => message.id === assistantId
            ? { ...message, activity: [...(message.activity ?? []).filter((item) => item !== status), status] }
            : message)),
          onToken: (token) => setMessages((current) => current.map((message) => message.id === assistantId
            ? { ...message, text: message.text + token }
            : message)),
          onMetadata: (res: ShopperChatResponse) => {
            setSessionId(res.sessionId);
            setCurrentIntent(res.structuredIntent);
            setMessages((current) => current.map((message) => message.id === assistantId
              ? { ...message, structuredIntent: res.structuredIntent, recommendations: res.recommendations, replySuggestions: res.replySuggestions }
              : message));
          },
        },
        1,
        expandedPrompt ? text : displayTextForConversation,
        attachments.filter((attachment) => attachment.base64Data).map((attachment) => ({
          fileName: attachment.name,
          mimeType: attachment.type,
          base64Data: attachment.base64Data!,
        }))
      );
    } catch {
      setMessages((current) => current.map((message) => message.id === assistantId ? {
        ...message,
        text: 'Apologies, our concierge service is momentarily unreachable. Please try again.',
        activity: [],
      } : message));
    } finally {
      setMessages((current) => current.map((message) => message.id === assistantId
        ? { ...message, isStreaming: false }
        : message));
      setLoading(false);
      window.setTimeout(() => chatInputRef.current?.focus(), 0);
    }
  };

  const addAttachments = async (files: FileList | File[]) => {
    const availableSlots = MAX_ATTACHMENTS - attachments.length;
    if (availableSlots <= 0) {
      setAttachmentError(`You can attach up to ${MAX_ATTACHMENTS} files.`);
      return;
    }

    const selected = Array.from(files).slice(0, availableSlots);
    const accepted: ChatAttachment[] = [];
    let error = Array.from(files).length > availableSlots ? `Only the first ${availableSlots} file${availableSlots === 1 ? '' : 's'} were added.` : '';

    for (const file of selected) {
      if (file.size > MAX_ATTACHMENT_BYTES) {
        error = `${file.name} is larger than 5 MB.`;
        continue;
      }

      const isText = file.type.startsWith('text/') || TEXT_ATTACHMENT_TYPES.has(file.type) || /\.(txt|md|csv|json)$/i.test(file.name);
      const attachment: ChatAttachment = {
        id: `${file.name}-${file.lastModified}-${crypto.randomUUID()}`,
        name: file.name,
        type: file.type || 'application/octet-stream',
        size: file.size,
      };

      if (file.type.startsWith('image/')) {
        attachment.previewUrl = URL.createObjectURL(file);
        const dataUrl = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => resolve(String(reader.result));
          reader.onerror = () => reject(reader.error);
          reader.readAsDataURL(file);
        });
        attachment.base64Data = dataUrl.split(',', 2)[1];
      }
      if (isText) attachment.textContent = (await file.text()).slice(0, 12_000);
      accepted.push(attachment);
    }

    setAttachments((current) => [...current, ...accepted]);
    setAttachmentError(error);
    if (fileInputRef.current) fileInputRef.current.value = '';
    window.setTimeout(() => chatInputRef.current?.focus(), 0);
  };

  const removeAttachment = (id: string) => {
    setAttachments((current) => {
      const removed = current.find((attachment) => attachment.id === id);
      if (removed?.previewUrl) URL.revokeObjectURL(removed.previewUrl);
      return current.filter((attachment) => attachment.id !== id);
    });
    setAttachmentError('');
    window.setTimeout(() => chatInputRef.current?.focus(), 0);
  };

  const startNewChat = () => {
    if (loading) return;
    setSessionId('');
    setCurrentIntent(null);
    setMessages([createWelcomeMessage()]);
    setInputQuery('');
    attachments.forEach((attachment) => attachment.previewUrl && URL.revokeObjectURL(attachment.previewUrl));
    setAttachments([]);
    setAttachmentError('');
    setMenuOpen(false);
    setShowHistory(false);
    setPendingDeleteSessionId(null);
    window.setTimeout(() => chatInputRef.current?.focus(), 0);
  };

  const openConversationHistory = async () => {
    setMenuOpen(false);
    setShowHistory(true);
    setPendingDeleteSessionId(null);
    setHistoryLoading(true);
    setHistoryError('');
    try {
      setConversationHistory(await getShopperConversationHistory());
    } catch {
      setHistoryError('Conversation history could not be loaded.');
    } finally {
      setHistoryLoading(false);
    }
  };

  const openConversation = async (selectedSessionId: string) => {
    if (loading) return;
    setHistoryLoading(true);
    setHistoryError('');
    try {
      const history = await getShopperConversation(selectedSessionId);
      setSessionId(history.sessionId);
      setCurrentIntent(history.latestIntent);
      setMessages(historyToMessages(history));
      setShowHistory(false);
      setPendingDeleteSessionId(null);
    } catch {
      setHistoryError('This conversation could not be opened.');
    } finally {
      setHistoryLoading(false);
    }
  };

  const removeConversation = async (selectedSessionId: string) => {
    setHistoryError('');
    try {
      await deleteShopperConversation(selectedSessionId);
      setConversationHistory((current) => current.filter((item) => item.sessionId !== selectedSessionId));
      setPendingDeleteSessionId(null);
      if (selectedSessionId === sessionId) startNewChat();
    } catch {
      setHistoryError('This conversation could not be deleted.');
    }
  };

  const startNavigation = (store: RecommendationCardDto) => {
    if (store.navigationNodeId) {
      if (onNavigateToNode) {
        onNavigateToNode(store.navigationNodeId, store.tenantName);
        return;
      }
      setDestinationNodeId(store.navigationNodeId);
      setNavigatingTo(store.tenantName);
      setActiveTab('navigate');
      calculatePath(startNodeId, store.navigationNodeId, accessibleOnly);
    }
  };

  const calculatePath = async (start: string, dest: string, accessible: boolean) => {
    if (!start || !dest) return;
    const requestId = ++navRequestIdRef.current;
    setNavLoading(true);
    setOverviewLoading(false);
    setRoute(null);
    try {
      const res = await getRoute(start, dest, undefined, accessible);
      if (requestId !== navRequestIdRef.current) return;
      setRoute(res);
      setNavLoading(false);
      if (!res.found) return;

      setOverviewLoading(true);
      try {
        const overview = await getRouteOverview(start, dest, undefined, accessible);
        if (requestId !== navRequestIdRef.current || !overview.found) return;
        setRoute((current) => current ? { ...current, conversationalDirections: overview.conversationalDirections } : current);
      } catch {
        // Keep the deterministic overview returned with the route.
      }
    } catch {
      // ignore
    } finally {
      if (requestId === navRequestIdRef.current) {
        setNavLoading(false);
        setOverviewLoading(false);
      }
    }
  };

  const latestRecommendations = [...messages]
    .reverse()
    .find((message) => message.recommendations?.length)?.recommendations ?? [];
  const latestAssistantId = [...messages].reverse().find((message) => message.sender === 'assistant')?.id;
  const conversationTitle = messages.find((message) => message.sender === 'user')?.text || 'New conversation';
  const visibleMessages = messages.filter((message) => message.id !== 'welcome');
  const isEmptyConversation = !visibleMessages.some((message) => message.sender === 'user');

  return (
    <div className="flex flex-col gap-6">
      {!navigationOnly && (!chatOnly || activeTab !== 'chat') && <section className="admin-page-header flex flex-wrap items-end justify-between gap-5 p-6 md:p-8">
        <div><h1 className="text-3xl font-bold tracking-[-0.04em]">{activeTab === 'chat' ? 'How can we help?' : 'Get directions'}</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-base-content/55">{activeTab === 'chat' ? 'Ask about stores, food, gifts or things to do.' : 'Choose where you are and where you want to go.'}</p></div>
        {!chatOnly && !navigationOnly && <div className="flex rounded-full border border-base-300 bg-base-100 p-1 shadow-sm">
          <button onClick={() => setActiveTab('chat')} className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-xs font-bold transition ${activeTab === 'chat' ? 'bg-primary text-primary-content' : 'text-base-content/55'}`}><Sparkles size={15} /> Ask</button>
          <button onClick={() => { setActiveTab('navigate'); if (startNodeId && destinationNodeId && !route) calculatePath(startNodeId, destinationNodeId, accessibleOnly); }} className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-xs font-bold transition ${activeTab === 'navigate' ? 'bg-secondary text-secondary-content' : 'text-base-content/55'}`}><Navigation size={15} /> Directions {navigatingTo && <span className="hidden sm:inline">· {navigatingTo}</span>}</button>
        </div>}
      </section>}

      {activeTab === 'chat' ? (
        <div style={{ display: 'grid', gridTemplateColumns: !chatOnly && latestRecommendations.length ? 'minmax(0, 1.7fr) minmax(280px, 0.9fr)' : '1fr', gap: '24px' }}>
          {/* Main Chat Stream */}
          <div className={`${chatOnly ? 'min-h-[calc(100vh-13rem)] bg-transparent' : 'minimal-card concierge-chat overflow-hidden'}`} style={{ position: 'relative', display: 'flex', flexDirection: 'column', height: chatOnly ? undefined : '680px' }}>
            <div className={`flex min-h-16 items-center justify-between gap-4 px-1 py-3 md:px-2 ${chatOnly ? 'chat-conversation-toolbar sticky z-30 bg-base-200/90 backdrop-blur-xl' : ''} ${showHistory ? 'border-b border-base-300/70' : ''}`}>
              <div className="min-w-0">
                <div className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.12em] text-primary"><Sparkles size={12} /> Sunway Kluang Mall AI</div>
                <h1 className="mt-1 truncate text-lg font-extrabold tracking-[-0.025em] text-base-content md:text-xl" title={showHistory ? 'Conversation history' : conversationTitle}>{showHistory ? 'Conversation history' : conversationTitle}</h1>
              </div>
              <div className="flex shrink-0 items-center gap-1">
                {showHistory ? <button type="button" className="btn btn-ghost btn-sm rounded-full px-3" onClick={() => setShowHistory(false)}><X size={16} /> <span className="hidden sm:inline">Close</span></button> : <>
                <button type="button" className="btn btn-ghost btn-sm hidden rounded-full px-3 sm:inline-flex" onClick={startNewChat} disabled={loading}><MessageSquarePlus size={16} /> New chat</button>
                <button type="button" className="btn btn-ghost btn-sm hidden rounded-full px-3 sm:inline-flex" onClick={() => void openConversationHistory()}><History size={16} /> History</button>
                <div className="relative sm:hidden">
                <button type="button" className="btn btn-ghost btn-sm btn-circle" aria-label="Conversation options" aria-expanded={menuOpen} onClick={() => setMenuOpen((open) => !open)}>
                  <EllipsisVertical size={19} />
                </button>
                {menuOpen && <div className="absolute right-0 top-11 z-30 w-52 rounded-xl border border-base-300 bg-white p-1.5 shadow-lg">
                  <button type="button" className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-semibold hover:bg-base-200" onClick={startNewChat} disabled={loading}><MessageSquarePlus size={16} /> New chat</button>
                  <button type="button" className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-semibold hover:bg-base-200" onClick={() => void openConversationHistory()}><History size={16} /> Conversation history</button>
                </div>}
                </div>
                </>}
              </div>
            </div>

            {showHistory ? <section className={`flex min-h-0 flex-1 flex-col ${chatOnly ? 'min-h-[calc(100vh-17rem)] bg-transparent' : 'bg-base-100'}`} aria-label="Conversation history">
              {!chatOnly && <div className="flex items-center justify-between border-b border-base-300 px-5 py-4">
                <div><h2 className="text-base font-bold">Conversation history</h2><p className="mt-0.5 text-xs text-base-content/50">Open or remove a previous chat.</p></div>
                <button type="button" className="btn btn-ghost btn-sm btn-circle" onClick={() => setShowHistory(false)} aria-label="Close conversation history"><X size={18} /></button>
              </div>}
              {chatOnly && <div className="mx-auto w-full max-w-3xl px-3 pb-3 pt-5 md:px-6"><p className="text-sm text-base-content/50">Open a previous conversation or remove chats you no longer need.</p><button type="button" className="btn btn-primary btn-sm mt-4 rounded-full px-5" onClick={startNewChat} disabled={loading}><MessageSquarePlus size={15} /> New chat</button></div>}
              <div className={`flex-1 p-3 ${chatOnly ? 'overflow-visible md:px-6' : 'overflow-y-auto'}`}>
                {historyLoading && <div className="grid h-full place-items-center text-sm text-base-content/50"><span className="loading loading-spinner loading-sm text-primary" /></div>}
                {!historyLoading && historyError && <div className="m-2 rounded-xl border border-error/20 bg-error/5 p-4 text-sm text-error">{historyError}</div>}
                {!historyLoading && !historyError && conversationHistory.length === 0 && <div className="grid h-full place-items-center px-6 text-center"><div><History className="mx-auto text-base-content/25" size={34} /><p className="mt-3 text-sm font-semibold">No saved conversations yet</p><p className="mt-1 text-xs text-base-content/50">Your chats will appear here after you send a message.</p><button type="button" className="btn btn-primary btn-sm mt-4" onClick={startNewChat}>Start a chat</button></div></div>}
                {!historyLoading && conversationHistory.length > 0 && <div className={`${chatOnly ? 'mx-auto max-w-3xl divide-y divide-base-300/70' : 'space-y-2'}`}>
                  {conversationHistory.map((conversation) => <article key={conversation.sessionId} className={`group transition ${chatOnly ? `px-2 py-4 ${conversation.sessionId === sessionId ? 'bg-primary/5' : 'hover:bg-base-100/70'}` : `rounded-xl border p-2 ${conversation.sessionId === sessionId ? 'border-primary/30 bg-primary/5' : 'border-base-300 bg-white hover:border-primary/25'}`}`}>
                    <div className="flex items-center gap-2">
                      <button type="button" className="min-w-0 flex-1 rounded-lg px-2 py-1.5 text-left" onClick={() => void openConversation(conversation.sessionId)}>
                        <div className="truncate text-sm font-bold">{conversation.title}</div>
                        <div className="mt-1 line-clamp-1 text-xs text-base-content/50">{conversation.lastMessage}</div>
                        <div className="mt-2 flex items-center gap-2 text-[11px] text-base-content/40"><span>{new Date(conversation.updatedAtUtc).toLocaleDateString([], { month: 'short', day: 'numeric' })}</span><span>·</span><span>{Math.ceil(conversation.turnCount / 2)} {conversation.turnCount <= 2 ? 'exchange' : 'exchanges'}</span></div>
                      </button>
                      <button type="button" className="btn btn-ghost btn-sm btn-circle text-base-content/40 hover:bg-error/10 hover:text-error" aria-label={`Delete ${conversation.title}`} onClick={() => setPendingDeleteSessionId(conversation.sessionId)}><Trash2 size={15} /></button>
                    </div>
                    {pendingDeleteSessionId === conversation.sessionId && <div className="mt-2 flex flex-wrap items-center justify-between gap-2 rounded-lg border border-error/15 bg-error/5 px-3 py-2">
                      <span className="text-xs font-semibold text-error">Delete this conversation?</span>
                      <div className="flex gap-1.5"><button type="button" className="btn btn-ghost btn-xs" onClick={() => setPendingDeleteSessionId(null)}>Cancel</button><button type="button" className="btn btn-error btn-xs" onClick={() => void removeConversation(conversation.sessionId)}>Delete</button></div>
                    </div>}
                  </article>)}
                </div>}
              </div>
              {!chatOnly && <div className="border-t border-base-300 bg-white p-3"><button type="button" className="btn btn-primary btn-sm w-full" onClick={startNewChat} disabled={loading}><MessageSquarePlus size={15} /> New chat</button></div>}
            </section> : <>
            {/* Message Thread */}
            <div ref={chatStreamRef} className={`min-h-0 flex-1 px-3 md:px-6 ${chatOnly ? 'overflow-visible' : 'overflow-y-auto'}`}>
              {isEmptyConversation ? <div className={`grid place-items-center pb-8 text-center ${chatOnly ? 'min-h-[calc(100vh-25rem)]' : 'h-full'}`}>
                <div className="max-w-xl px-5">
                  <span className="mx-auto grid size-16 place-items-center rounded-3xl bg-gradient-to-br from-primary to-secondary text-primary-content shadow-lg shadow-primary/20"><Sparkles size={27} /></span>
                  <h1 className="mt-6 text-3xl font-extrabold tracking-[-0.04em] text-base-content md:text-4xl">How can I help with your visit?</h1>
                  <p className="mx-auto mt-3 max-w-lg text-sm leading-6 text-base-content/55 md:text-base">Ask naturally about stores, dining, gifts, mall facilities, promotions, or the quickest way to get somewhere.</p>
                  <div className="mt-7 flex items-center justify-center gap-2 text-xs font-medium text-base-content/40"><span className="size-1.5 rounded-full bg-success" /> Ready to help with your mall visit</div>
                </div>
              </div> : <div className="mx-auto w-full max-w-3xl space-y-7 py-6">
                {visibleMessages.map((m) => <div key={m.id} className={`flex items-start gap-3 ${m.sender === 'user' ? 'justify-end' : 'justify-start'}`}>
                  {m.sender === 'assistant' && <span className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary"><Sparkles size={15} /></span>}
                  <div className={`flex max-w-[85%] flex-col ${m.sender === 'user' ? 'items-end' : 'items-start'}`}>
                    <div className={`text-sm leading-6 ${m.sender === 'user' ? 'rounded-3xl rounded-br-lg bg-neutral px-5 py-3 text-neutral-content shadow-sm' : 'px-1 py-1 text-base-content'}`}>
                      {m.sender === 'assistant'
                        ? m.text
                          ? <><AgentMarkdown compact>{m.text}</AgentMarkdown>{m.id === latestAssistantId && !m.isStreaming && Boolean(m.replySuggestions?.length) && <div className="mt-4 flex flex-wrap gap-2" aria-label="Suggested replies">{m.replySuggestions?.map((suggestion) => <button key={suggestion.label} type="button" onClick={() => void handleSend(suggestion.label, suggestion.prompt)} disabled={loading} className="btn btn-sm h-9 min-h-9 rounded-full border border-primary/20 bg-white px-3 font-semibold text-primary shadow-sm hover:bg-primary/5"><Sparkles size={12} /> {suggestion.label}</button>)}</div>}</>
                          : m.isStreaming
                            ? <span className="inline-flex items-center gap-2 text-sm font-medium text-base-content/55"><span className="loading loading-dots loading-sm text-primary" />{m.activity?.at(-1) || 'Thinking…'}</span>
                            : null
                        : <><p className="whitespace-pre-wrap">{m.text}</p>{Boolean(m.attachments?.length) && <div className="mt-3 grid gap-2">{m.attachments?.map((attachment) => <div key={attachment.id} className="flex items-center gap-2 rounded-xl bg-white/10 px-3 py-2 text-xs">{attachment.previewUrl ? <img src={attachment.previewUrl} alt="" className="size-9 rounded-lg object-cover" /> : <FileText size={16} />}<span className="min-w-0 truncate">{attachment.name}</span></div>)}</div>}</>}
                    </div>
                    <span className="mt-1 px-1 text-[10px] text-base-content/30">{m.timestamp}</span>
                  </div>
                </div>)}
              </div>}
            </div>

            {/* Input Form */}
            <form
              onSubmit={(e) => {
                e.preventDefault();
                handleSend();
              }}
              className={`mx-auto w-[calc(100%_-_1.5rem)] max-w-3xl rounded-[1.5rem] border border-base-300 bg-white p-2 shadow-[0_12px_35px_rgba(15,23,42,0.10)] transition focus-within:border-primary/45 focus-within:shadow-[0_14px_40px_rgba(15,23,42,0.13)] ${chatOnly ? 'sticky bottom-4 z-20 mb-4' : 'mb-3 md:mb-5'}`}
            >
              {Boolean(attachments.length) && <div className="flex flex-wrap gap-2 px-2 pb-2 pt-1" aria-label="Attached files">{attachments.map((attachment) => <div key={attachment.id} className="group flex max-w-full items-center gap-2 rounded-xl border border-base-300 bg-base-200/70 p-1.5 pr-2 text-xs font-medium">{attachment.previewUrl ? <img src={attachment.previewUrl} alt="" className="size-9 rounded-lg object-cover" /> : <span className="grid size-9 place-items-center rounded-lg bg-white text-primary"><FileText size={16} /></span>}<span className="max-w-40 truncate">{attachment.name}</span><button type="button" className="btn btn-ghost btn-xs btn-circle" onClick={() => removeAttachment(attachment.id)} aria-label={`Remove ${attachment.name}`}><X size={13} /></button></div>)}</div>}
              {attachmentError && <p className="px-3 pb-1 text-xs font-medium text-error" role="alert">{attachmentError}</p>}
              <div className="flex items-end gap-1">
                <input ref={fileInputRef} type="file" className="hidden" multiple accept="image/*,.txt,.md,.csv,.json" onChange={(event) => event.target.files && void addAttachments(event.target.files)} />
                <button type="button" className="btn btn-ghost btn-circle shrink-0 text-base-content/55 hover:bg-primary/10 hover:text-primary" onClick={() => fileInputRef.current?.click()} disabled={loading || attachments.length >= MAX_ATTACHMENTS} aria-label="Attach files" title="Attach image or file"><Paperclip size={18} /></button>
                <textarea
                  ref={chatInputRef}
                  autoFocus
                  rows={1}
                  placeholder="Message Mall AI…"
                  value={inputQuery}
                  onChange={(e) => setInputQuery(e.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' && !event.shiftKey) {
                      event.preventDefault();
                      if (!loading && historyLoaded && userId && (inputQuery.trim() || attachments.length)) void handleSend();
                    }
                  }}
                  className="max-h-36 min-h-11 min-w-0 flex-1 resize-none bg-transparent px-2 py-3 text-sm leading-5 outline-none placeholder:text-base-content/35"
                  aria-label="Message Mall AI"
                />
                <button
                  type="submit"
                  disabled={loading || !historyLoaded || !userId || (!inputQuery.trim() && attachments.length === 0)}
                  className="btn btn-primary btn-circle shrink-0 disabled:bg-base-200 disabled:text-base-content/25"
                  aria-label="Send message"
                >
                  <Send size={16} />
                </button>
              </div>
            </form>
            </>}
          </div>

          {!chatOnly && latestRecommendations.length > 0 && <div className="concierge-results" style={{ display: 'flex', flexDirection: 'column', gap: '16px', maxHeight: '560px', overflowY: 'auto' }}>
            <h2 className="text-base font-bold">Suggestions</h2>

            {latestRecommendations.map((card) => (
              <div key={card.tenantId} className="minimal-card minimal-card-hover" style={{ padding: '20px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div>
                    <h4 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-primary)' }}>{card.tenantName}</h4>
                    <span style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '2px' }}>
                      <MapPin size={12} /> Level {card.floorNumber} • Unit {card.unitNumber}
                    </span>
                  </div>
                  <span className="mono-tag" style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)', padding: '2px 8px', borderRadius: '4px' }}>
                    Level {card.floorNumber}
                  </span>
                </div>

                <p style={{ fontSize: '13px', color: '#52525b', lineHeight: '1.4' }}>
                  {card.description}
                </p>

                <div style={{ background: '#f4f4f5', borderLeft: '3px solid var(--text-primary)', padding: '8px 10px', borderRadius: '4px', fontSize: '12px', color: 'var(--text-primary)' }}>
                  {card.whyRecommended}
                </div>

                {/* Active promotion */}
                {card.activePromotionTitle && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px', background: '#fef3c7', border: '1px solid #fde68a', padding: '6px 10px', borderRadius: '6px', fontSize: '12px', color: '#92400e' }}>
                    <Tag size={13} />
                    <span><strong>{card.activePromotionTitle}:</strong> {card.activePromotionOffer}</span>
                  </div>
                )}

                <button
                  onClick={() => startNavigation(card)}
                  className="btn-minimal-primary"
                  style={{ width: '100%', padding: '9px 12px', fontSize: '13px', marginTop: '4px' }}
                >
                  <Navigation size={14} /> Get directions <ArrowRight size={14} />
                </button>
              </div>
            ))}
          </div>}
        </div>
      ) : (
        /* Landmark Indoor Navigation Tab */
        <div className="grid gap-6 lg:min-h-[calc(100vh-18rem)] lg:grid-cols-[minmax(320px,0.9fr)_minmax(0,2.1fr)]">
          {/* Controls */}
          <div className="minimal-card flex flex-col gap-6 p-6 md:p-8">
            <h3 style={{ fontSize: '16px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Compass size={18} /> Plan your route
            </h3>

            {/* Starting Location */}
            <div>
              <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: '6px' }}>
                Origin Landmark (Where are you?):
              </label>
              <select
                value={startNodeId}
                onChange={(e) => {
                  setStartNodeId(e.target.value);
                  if (destinationNodeId) calculatePath(e.target.value, destinationNodeId, accessibleOnly);
                }}
                style={{
                  width: '100%',
                  background: '#ffffff',
                  color: 'var(--text-primary)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-sm)',
                  padding: '10px 12px',
                  fontSize: '13px',
                }}
              >
                {nodes.map((n) => (
                  <option key={n.id} value={n.id}>
                    Level {n.floorNumber}: {n.label}
                  </option>
                ))}
              </select>
            </div>

            {/* Destination */}
            <div>
              <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: '6px' }}>
                Destination:
              </label>
              <select
                value={destinationNodeId}
                onChange={(e) => {
                  setDestinationNodeId(e.target.value);
                  if (startNodeId) calculatePath(startNodeId, e.target.value, accessibleOnly);
                }}
                style={{
                  width: '100%',
                  background: '#ffffff',
                  color: 'var(--text-primary)',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: 'var(--radius-sm)',
                  padding: '10px 12px',
                  fontSize: '13px',
                }}
              >
                {nodes.map((n) => (
                  <option key={n.id} value={n.id}>
                    Level {n.floorNumber}: {n.label}
                  </option>
                ))}
              </select>
            </div>

            {/* Accessibility Toggle */}
            <div
              onClick={() => {
                const next = !accessibleOnly;
                setAccessibleOnly(next);
                if (startNodeId && destinationNodeId) calculatePath(startNodeId, destinationNodeId, next);
              }}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '12px',
                padding: '12px 14px',
                background: accessibleOnly ? '#eff6ff' : 'var(--bg-secondary)',
                border: `1px solid ${accessibleOnly ? '#93c5fd' : 'var(--border-subtle)'}`,
                borderRadius: 'var(--radius-sm)',
                cursor: 'pointer',
              }}
            >
              <Accessibility size={20} color={accessibleOnly ? '#1d4ed8' : '#71717a'} />
              <div>
                <div style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-primary)' }}>
                  Stroller & Wheelchair Accessible
                </div>
                <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                  Routes via central elevators and eliminates escalators
                </div>
              </div>
            </div>

            <button
              onClick={() => calculatePath(startNodeId, destinationNodeId, accessibleOnly)}
              disabled={navLoading}
              className="btn-minimal-primary"
              style={{ padding: '12px' }}
            >
              <Navigation size={15} /> {navLoading ? 'Calculating route...' : 'Calculate Route'}
            </button>
          </div>

          {/* Navigation Output */}
          <div className="minimal-card flex min-h-[32rem] flex-col gap-6 p-6 md:p-8 lg:min-h-0">
            {route ? (
              <>
                <div style={{ display: 'flex', gap: '24px', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '16px' }}>
                  <div>
                    <div className="mono-tag" style={{ color: 'var(--text-muted)' }}>WALKING DISTANCE</div>
                    <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-primary)' }}>{route.totalDistanceMeters} m</div>
                  </div>
                  <div>
                    <div className="mono-tag" style={{ color: 'var(--text-muted)' }}>FLOOR TRANSITIONS</div>
                    <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-primary)' }}>{route.floorChanges} floor(s)</div>
                  </div>
                </div>

                {/* Landmark overview loads independently so it never blocks the route result. */}
                <div style={{ background: '#f8fafc', border: '1px solid var(--border-subtle)', padding: '16px 20px', borderRadius: 'var(--radius-sm)' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: '#4338ca', fontWeight: 700, marginBottom: '6px' }}>
                    <Sparkles size={14} /> ROUTE OVERVIEW
                  </div>
                  <p style={{ fontSize: '14px', color: 'var(--text-primary)', lineHeight: '1.6' }}>
                    {overviewLoading ? 'Preparing a landmark-based overview…' : route.conversationalDirections}
                  </p>
                </div>

                {/* Step sequence */}
                <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  Waypoint Steps:
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', maxHeight: '440px', overflowY: 'auto' }}>
                  {route.steps.map((step, idx) => (
                    <div
                      key={idx}
                      style={{
                        display: 'flex',
                        alignItems: 'flex-start',
                        gap: '12px',
                        background: '#ffffff',
                        padding: '14px 16px',
                        borderRadius: 'var(--radius-sm)',
                        border: '1px solid var(--border-subtle)',
                      }}
                    >
                      <span
                        style={{
                          background: 'var(--text-primary)',
                          color: '#ffffff',
                          width: '22px',
                          height: '22px',
                          borderRadius: '50%',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          fontSize: '11px',
                          fontWeight: 700,
                          flexShrink: 0,
                        }}
                      >
                        {idx + 1}
                      </span>
                      <div style={{ flex: 1 }}>
                        <div style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-primary)' }}>
                          {step.instruction}
                        </div>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '2px' }}>
                          {step.fromNodeLabel} → {step.toNodeLabel} ({step.distanceMeters}m)
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <div style={{ display: 'grid', flex: 1, placeItems: 'center', textAlign: 'center', color: 'var(--text-muted)', padding: '96px 24px' }}>
                Select an origin and destination to solve wayfinding route.
              </div>
            )}
          </div>
        </div>
      )}

    </div>
  );
};
