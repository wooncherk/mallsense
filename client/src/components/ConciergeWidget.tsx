import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { ArrowRight, Bot, Expand, FileText, MapPin, MessageCircle, Paperclip, Send, Sparkles, X } from 'lucide-react';
import { getLatestShopperConversation, streamShopperAi } from '../api';
import type { RecommendationCardDto, ShopperChatResponse, ShopperReplySuggestionDto, StructuredIntentDto } from '../types';
import { AgentMarkdown } from './AgentMarkdown';

interface WidgetMessage {
  id: string;
  sender: 'user' | 'assistant';
  text: string;
  recommendations?: RecommendationCardDto[];
  activity?: string[];
  isStreaming?: boolean;
  replySuggestions?: ShopperReplySuggestionDto[];
  attachments?: WidgetAttachment[];
}

interface WidgetAttachment {
  id: string;
  name: string;
  type: string;
  previewUrl?: string;
  textContent?: string;
  base64Data?: string;
}

const MAX_ATTACHMENTS = 4;
const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;
const TEXT_ATTACHMENT_TYPES = new Set(['application/json', 'text/csv', 'text/markdown', 'text/plain']);

interface ConciergeWidgetProps {
  open: boolean;
  initialQuery?: string;
  onOpen: () => void;
  onClose: () => void;
  onOpenPage: () => void;
  onNavigate: (nodeId: string, destinationName: string) => void;
  userId?: string;
}

const welcome: WidgetMessage = {
  id: 'welcome',
  sender: 'assistant',
  text: 'What can I help you find? Ask about a store, product, meal, facility, or directions.',
};

export function ConciergeWidget({ open, initialQuery, onOpen, onClose, onOpenPage, onNavigate, userId }: ConciergeWidgetProps) {
  const [messages, setMessages] = useState<WidgetMessage[]>([welcome]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [sessionId, setSessionId] = useState<string>();
  const [intent, setIntent] = useState<StructuredIntentDto | null>(null);
  const [historyLoaded, setHistoryLoaded] = useState(false);
  const [attachments, setAttachments] = useState<WidgetAttachment[]>([]);
  const [attachmentError, setAttachmentError] = useState('');
  const lastInitialQuery = useRef<string | undefined>(undefined);
  const streamRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!userId) return;
    let cancelled = false;
    setHistoryLoaded(false);
    getLatestShopperConversation()
      .then((history) => {
        if (cancelled || !history) return;
        setSessionId(history.sessionId);
        setIntent(history.latestIntent);
        setMessages(history.turns.map((turn) => ({
          id: turn.id,
          sender: turn.role,
          text: turn.message,
          recommendations: turn.recommendations,
          replySuggestions: turn.replySuggestions,
        })));
      })
      .catch(() => undefined)
      .finally(() => { if (!cancelled) setHistoryLoaded(true); });
    return () => { cancelled = true; };
  }, [userId]);

  const send = async (text: string, expandedPrompt?: string) => {
    const query = text.trim();
    const displayText = query || 'Please review the attached file.';
    const attachmentContext = attachments.map((attachment) => attachment.textContent
      ? `Attached file: ${attachment.name}\n${attachment.textContent}`
      : `Attached ${attachment.type.startsWith('image/') ? 'image' : 'file'}: ${attachment.name} (${attachment.type || 'unknown type'}).`
    ).join('\n\n');
    const prompt = [expandedPrompt || query, attachmentContext].filter(Boolean).join('\n\n').trim();
    if ((!query && attachments.length === 0) || !prompt || loading || !userId || !historyLoaded) return;

    const assistantId = `${Date.now()}-assistant`;
    const conversation = messages.slice(-8).map((message) => ({ role: message.sender, message: message.text }));
    setMessages((current) => [...current,
      { id: `${Date.now()}-user`, sender: 'user', text: displayText, attachments },
      { id: assistantId, sender: 'assistant', text: '', activity: [], isStreaming: true },
    ]);
    setInput('');
    setAttachments([]);
    setAttachmentError('');
    setLoading(true);
    try {
      await streamShopperAi(prompt, intent, sessionId, conversation, {
        onStatus: (status) => setMessages((current) => current.map((message) => message.id === assistantId
          ? { ...message, activity: [...(message.activity ?? []).filter((item) => item !== status), status] }
          : message)),
        onToken: (token) => setMessages((current) => current.map((message) => message.id === assistantId
          ? { ...message, text: message.text + token }
          : message)),
        onMetadata: (result: ShopperChatResponse) => {
          setSessionId(result.sessionId);
          setIntent(result.structuredIntent);
          setMessages((current) => current.map((message) => message.id === assistantId
            ? { ...message, recommendations: result.recommendations, replySuggestions: result.replySuggestions }
            : message));
        },
      }, 1, expandedPrompt ? query : displayText, attachments.filter((attachment) => attachment.base64Data).map((attachment) => ({
        fileName: attachment.name,
        mimeType: attachment.type,
        base64Data: attachment.base64Data!,
      })));
    } catch {
      setMessages((current) => current.map((message) => message.id === assistantId ? {
        ...message,
        text: 'I could not reach the mall service. Please try again.',
        activity: [],
      } : message));
    } finally {
      setMessages((current) => current.map((message) => message.id === assistantId
        ? { ...message, isStreaming: false }
        : message));
      setLoading(false);
      window.setTimeout(() => inputRef.current?.focus(), 0);
    }
  };

  const addAttachments = async (files: FileList) => {
    const availableSlots = MAX_ATTACHMENTS - attachments.length;
    if (availableSlots <= 0) {
      setAttachmentError(`You can attach up to ${MAX_ATTACHMENTS} files.`);
      return;
    }

    const selected = Array.from(files).slice(0, availableSlots);
    const accepted: WidgetAttachment[] = [];
    let error = files.length > availableSlots ? `Only the first ${availableSlots} file${availableSlots === 1 ? '' : 's'} were added.` : '';

    for (const file of selected) {
      if (file.size > MAX_ATTACHMENT_BYTES) {
        error = `${file.name} is larger than 5 MB.`;
        continue;
      }

      const attachment: WidgetAttachment = {
        id: `${file.name}-${file.lastModified}-${crypto.randomUUID()}`,
        name: file.name,
        type: file.type || 'application/octet-stream',
      };
      const isText = file.type.startsWith('text/') || TEXT_ATTACHMENT_TYPES.has(file.type) || /\.(txt|md|csv|json)$/i.test(file.name);

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
    window.setTimeout(() => inputRef.current?.focus(), 0);
  };

  const removeAttachment = (id: string) => {
    setAttachments((current) => {
      const removed = current.find((attachment) => attachment.id === id);
      if (removed?.previewUrl) URL.revokeObjectURL(removed.previewUrl);
      return current.filter((attachment) => attachment.id !== id);
    });
    setAttachmentError('');
    window.setTimeout(() => inputRef.current?.focus(), 0);
  };

  useEffect(() => {
    if (!historyLoaded || !open || !initialQuery || initialQuery === lastInitialQuery.current) return;
    lastInitialQuery.current = initialQuery;
    void send(initialQuery);
  }, [historyLoaded, initialQuery, open]);

  useEffect(() => {
    streamRef.current?.scrollTo({ top: streamRef.current.scrollHeight, behavior: 'smooth' });
  }, [loading, messages]);

  useEffect(() => {
    if (!open) return;
    const closeOnEscape = (event: KeyboardEvent) => event.key === 'Escape' && onClose();
    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [onClose, open]);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    void send(input);
  };

  if (!open) {
    return <button className="btn fixed bottom-5 right-5 z-50 rounded-full border-0 bg-primary px-4 text-primary-content shadow-md hover:bg-primary/90 sm:px-5" onClick={onOpen} aria-label="Ask Sunway Kluang Mall AI"><MessageCircle size={18} /><span className="hidden sm:inline">Ask AI</span></button>;
  }

  return (
    <aside className="fixed inset-x-3 bottom-3 z-50 flex h-[min(620px,calc(100vh-24px))] flex-col overflow-hidden rounded-3xl border border-primary/15 bg-white shadow-lg sm:left-auto sm:right-5 sm:w-[420px]" aria-label="Sunway Kluang Mall assistant">
      <header className="flex items-center justify-between border-b border-primary/10 bg-white px-5 py-4">
        <div className="flex items-center gap-3"><span className="grid size-10 place-items-center rounded-xl bg-primary text-primary-content"><Bot size={19} /></span><div><h2 className="font-bold text-[#173A44]">Sunway Kluang Mall AI</h2><p className="text-xs text-[#55777F]">Stores, deals, events and visitor help</p></div></div>
        <div className="flex items-center gap-1"><button className="btn btn-ghost btn-sm btn-circle text-primary hover:bg-primary/5" onClick={onOpenPage} aria-label="Open full AI page" title="Open full AI page"><Expand size={17} /></button><button className="btn btn-ghost btn-sm btn-circle text-primary hover:bg-primary/5" onClick={onClose} aria-label="Close assistant"><X size={18} /></button></div>
      </header>

      <div ref={streamRef} className="flex-1 space-y-4 overflow-y-auto bg-white p-4">
        {messages.map((message) => (
          <div key={message.id} className={`flex items-end gap-2 ${message.sender === 'user' ? 'justify-end' : 'justify-start'}`}>
            {message.sender === 'assistant' && <span className="grid size-7 shrink-0 place-items-center rounded-full bg-primary/10 text-primary"><Sparkles size={13} /></span>}
            <div className={`max-w-[84%] rounded-2xl px-4 py-3 text-sm leading-6 ${message.sender === 'user' ? 'rounded-br-md bg-primary text-primary-content' : 'rounded-bl-md border border-primary/15 bg-primary/5 text-[#173A44]'}`}>
              {message.sender === 'assistant'
                ? message.text
                  ? <AgentMarkdown compact>{message.text}</AgentMarkdown>
                  : message.isStreaming
                    ? <span className="inline-flex items-center gap-2 text-xs font-medium text-[#55777F]"><span className="loading loading-spinner loading-xs text-primary" />{message.activity?.at(-1) || 'Thinking…'}</span>
                    : null
                : <p className="whitespace-pre-line">{message.text}</p>}
              {Boolean(message.attachments?.length) && <div className="mt-2 grid gap-1.5">{message.attachments?.map((attachment) => <div key={attachment.id} className="flex items-center gap-2 rounded-lg bg-white/15 px-2 py-1.5 text-xs">{attachment.previewUrl ? <img src={attachment.previewUrl} alt="" className="size-8 rounded-md object-cover" /> : <FileText size={14} />}<span className="min-w-0 truncate">{attachment.name}</span></div>)}</div>}
              {message.recommendations?.map((store) => (
                <div key={store.tenantId} className="mt-3 rounded-xl border border-primary/15 bg-white p-3 text-[#173A44]">
                  <div className="flex items-start justify-between gap-2"><div><strong>{store.tenantName}</strong><div className="mt-1 flex items-center gap-1 text-xs text-[#55777F]"><MapPin size={12} /> Level {store.floorNumber} · {store.unitNumber}</div></div>{store.navigationNodeId && <button className="btn btn-primary btn-xs rounded-full" onClick={() => onNavigate(store.navigationNodeId!, store.tenantName)}>Directions <ArrowRight size={11} /></button>}</div>
                  <p className="mt-2 text-xs leading-5 text-[#55777F]">{store.whyRecommended}</p>
                </div>
              ))}
              {message.id === messages.at(-1)?.id && !message.isStreaming && Boolean(message.replySuggestions?.length) && <div className="mt-3 flex flex-wrap gap-2 border-t border-primary/10 pt-3" aria-label="Suggested replies">
                {message.replySuggestions?.map((suggestion) => <button key={suggestion.label} type="button" className="btn btn-sm h-8 min-h-8 rounded-full border border-primary/20 bg-white px-3 font-semibold text-primary hover:bg-primary/5" disabled={loading} onClick={() => void send(suggestion.label, suggestion.prompt)}><Sparkles size={11} />{suggestion.label}</button>)}
              </div>}
            </div>
          </div>
        ))}
      </div>

      {messages.length === 1 && <div className="flex flex-wrap gap-2 border-t border-primary/10 bg-white px-4 py-3">
        {['Food near the cinema', 'Where is the restroom?'].map((prompt) => <button key={prompt} className="btn btn-sm h-8 min-h-8 rounded-full border border-primary/15 bg-white px-3 font-medium text-primary hover:bg-primary/5" onClick={() => void send(prompt)}><Sparkles size={11} />{prompt}</button>)}
      </div>}

      <form className="border-t border-primary/10 bg-white p-4" onSubmit={submit}>
        {Boolean(attachments.length) && <div className="mb-2 flex flex-wrap gap-2">{attachments.map((attachment) => <div key={attachment.id} className="flex max-w-full items-center gap-1.5 rounded-lg border border-primary/15 bg-primary/5 p-1 pr-1.5 text-xs font-medium text-[#173A44]">{attachment.previewUrl ? <img src={attachment.previewUrl} alt="" className="size-8 rounded-md object-cover" /> : <span className="grid size-8 place-items-center rounded-md bg-white text-primary"><FileText size={14} /></span>}<span className="max-w-32 truncate">{attachment.name}</span><button type="button" className="btn btn-ghost btn-xs btn-circle" onClick={() => removeAttachment(attachment.id)} aria-label={`Remove ${attachment.name}`}><X size={12} /></button></div>)}</div>}
        {attachmentError && <p className="mb-2 text-xs font-medium text-error" role="alert">{attachmentError}</p>}
        <div className="flex gap-2">
          <input ref={fileInputRef} type="file" className="hidden" multiple accept="image/*,.txt,.md,.csv,.json" onChange={(event) => event.target.files && void addAttachments(event.target.files)} />
          <button type="button" className="btn btn-ghost btn-square rounded-xl text-[#55777F] hover:bg-primary/5 hover:text-primary" onClick={() => fileInputRef.current?.click()} disabled={loading || attachments.length >= MAX_ATTACHMENTS} aria-label="Attach files" title="Attach image or file"><Paperclip size={17} /></button>
          <input ref={inputRef} autoFocus className="input min-w-0 flex-1 rounded-xl border-primary/20 bg-white text-[#173A44] outline-none placeholder:text-[#55777F] focus:border-primary/50 focus:outline-none" value={input} onChange={(event) => setInput(event.target.value)} placeholder="Ask what you need…" />
          <button className="btn btn-primary btn-square rounded-xl disabled:bg-primary/10 disabled:text-primary/30" disabled={loading || !historyLoaded || !userId || (!input.trim() && attachments.length === 0)} aria-label="Send"><Send size={17} /></button>
        </div>
      </form>
    </aside>
  );
}
