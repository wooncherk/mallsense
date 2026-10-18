import type {
  ShopperChatResponse,
  StructuredIntentDto,
  RouteResponse,
  RouteOverviewResponse,
  LocationRecoveryResponse,
  PosterExtractionResultDto,
  PromotionDto,
  EnrichmentClarificationResponse,
  IntentAnalyticsSummaryDto,
  BiInsightReportDto,
  NavigationNode,
  NavigationLocation,
  StoreDto,
  AuthResponse,
  UserProfileDto,
  ManagedUserDto,
  UserRole,
  AgentActionMode,
  KnowledgeCollectionDto,
  KnowledgeSourceDto,
  KnowledgeSearchResponse,
  PublicKnowledgeSourceDto,
  FloorPlanDraftDto,
  NavigationEdge,
  StorefrontSearchResponse,
  SaveNavigationNodeInput,
  SaveNavigationEdgeInput,
  NavigationGraphValidation,
  NavigationAiDraft,
  NavigationAiTurn,
  ShopperConversationTurnDto,
  ShopperConversationHistoryDto,
  ShopperConversationSummaryDto,
  DailyBiSummaryDto,
  BiNarrativeDto,
  BiSummaryHistoryDto,
  BiChatResponse,
  BiChatTurnDto,
  BiConversationHistoryDto,
  BiConversationSummaryDto,
  ShopperBehaviorEventInput,
} from './types';

const API_BASE = '/api';
const AUTH_TOKEN_KEY = 'mallsense_access_token';
const GUEST_DEVICE_KEY = 'mallsense_guest_device';
const BEHAVIOR_SESSION_KEY = 'mallsense_behavior_session';

export function getBehaviorSessionId(): string {
  let sessionId = sessionStorage.getItem(BEHAVIOR_SESSION_KEY);
  if (!sessionId) {
    sessionId = crypto.randomUUID().replaceAll('-', '');
    sessionStorage.setItem(BEHAVIOR_SESSION_KEY, sessionId);
  }
  return sessionId;
}

function storeAuthToken(accessToken: string): void {
  localStorage.setItem(AUTH_TOKEN_KEY, accessToken);
}

function getAuthHeaders(): Record<string, string> {
  const accessToken = localStorage.getItem(AUTH_TOKEN_KEY);
  return accessToken ? { Authorization: `Bearer ${accessToken}` } : {};
}

export function clearAuthSession(): void {
  localStorage.removeItem(AUTH_TOKEN_KEY);
}

export async function loginUser(email: string, password: string): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw new Error('Invalid email or password');
  const response: AuthResponse = await res.json();
  storeAuthToken(response.accessToken);
  return response;
}

export async function registerShopper(fullName: string, email: string, password: string): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ fullName, email, password }),
  });
  if (!res.ok) {
    const payload = await res.json().catch(() => null) as { error?: string } | null;
    throw new Error(payload?.error || 'Unable to create account');
  }
  const response: AuthResponse = await res.json();
  storeAuthToken(response.accessToken);
  return response;
}

export async function createGuestSession(deviceHint?: string): Promise<AuthResponse> {
  let persistentHint = deviceHint;
  if (!persistentHint) {
    persistentHint = localStorage.getItem(GUEST_DEVICE_KEY) || crypto.randomUUID();
    localStorage.setItem(GUEST_DEVICE_KEY, persistentHint);
  }
  const res = await fetch(`${API_BASE}/auth/guest-session`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ deviceHint: persistentHint }),
  });
  if (!res.ok) throw new Error('Failed to create guest session');
  const response: AuthResponse = await res.json();
  storeAuthToken(response.accessToken);
  return response;
}

export async function getCurrentUser(): Promise<UserProfileDto> {
  const res = await fetch(`${API_BASE}/auth/me`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load the current user');
  return res.json();
}

export async function checkHealth() {
  const res = await fetch(`${API_BASE}/health`);
  return res.json();
}

export async function chatWithShopperAi(
  message: string,
  previousIntent?: StructuredIntentDto | null,
  sessionId?: string,
  currentFloor: number = 1,
  displayMessage?: string,
  attachments: ShopperChatAttachment[] = []
): Promise<ShopperChatResponse> {
  const res = await fetch(`${API_BASE}/shopper/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({
      sessionId: sessionId || null,
      message,
      currentFloor,
      previousIntent: previousIntent || null,
      surfaceContext: 'AskAI',
      displayMessage: displayMessage || null,
      attachments,
    }),
  });
  if (!res.ok) throw new Error('Failed to process shopper message');
  return res.json();
}

export async function getLatestShopperConversation(): Promise<ShopperConversationHistoryDto | null> {
  const res = await fetch(`${API_BASE}/shopper/chat/latest`, { headers: getAuthHeaders() });
  if (res.status === 204) return null;
  if (!res.ok) throw new Error('Failed to load conversation history');
  return res.json();
}

export async function getShopperConversationHistory(): Promise<ShopperConversationSummaryDto[]> {
  const res = await fetch(`${API_BASE}/shopper/chat/history`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load conversation history');
  return res.json();
}

export async function getShopperConversation(sessionId: string): Promise<ShopperConversationHistoryDto> {
  const res = await fetch(`${API_BASE}/shopper/chat/${encodeURIComponent(sessionId)}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load conversation');
  return res.json();
}

export async function deleteShopperConversation(sessionId: string): Promise<void> {
  const res = await fetch(`${API_BASE}/shopper/chat/${encodeURIComponent(sessionId)}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });
  if (!res.ok && res.status !== 404) throw new Error('Failed to delete conversation');
}

export interface ShopperChatStreamHandlers {
  onStatus: (message: string) => void;
  onToken: (text: string) => void;
  onMetadata: (response: ShopperChatResponse) => void;
}

export interface ShopperChatAttachment {
  fileName: string;
  mimeType: string;
  base64Data: string;
}

export async function streamShopperAi(
  message: string,
  previousIntent: StructuredIntentDto | null | undefined,
  sessionId: string | undefined,
  conversation: ShopperConversationTurnDto[],
  handlers: ShopperChatStreamHandlers,
  currentFloor: number = 1,
  displayMessage?: string,
  attachments: ShopperChatAttachment[] = []
): Promise<void> {
  const res = await fetch(`${API_BASE}/shopper/chat/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({
      sessionId: sessionId || null,
      message,
      currentFloor,
      previousIntent: previousIntent || null,
      surfaceContext: 'AskAI',
      conversation,
      displayMessage: displayMessage || null,
      attachments,
    }),
  });
  if (!res.ok || !res.body) {
    // Keeps chat usable while a previously running API instance is being restarted during deployment.
    handlers.onStatus('Preparing your answer');
    const fallback = await chatWithShopperAi(message, previousIntent, sessionId, currentFloor, displayMessage, attachments);
    handlers.onMetadata({ ...fallback, reply: '' });
    handlers.onToken(fallback.reply);
    return;
  }

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffered = '';
  const consumeLine = (line: string) => {
    if (!line.trim()) return;
    const event = JSON.parse(line) as {
      type: 'status' | 'token' | 'metadata' | 'done';
      message?: string;
      text?: string;
      response?: ShopperChatResponse;
    };
    if (event.type === 'status' && event.message) handlers.onStatus(event.message);
    if (event.type === 'token' && event.text) handlers.onToken(event.text);
    if (event.type === 'metadata' && event.response) handlers.onMetadata(event.response);
  };

  while (true) {
    const { done, value } = await reader.read();
    buffered += decoder.decode(value, { stream: !done });
    const lines = buffered.split('\n');
    buffered = lines.pop() ?? '';
    lines.forEach(consumeLine);
    if (done) break;
  }
  if (buffered.trim()) consumeLine(buffered);
}

export async function getRoute(
  startNodeId: string,
  destinationNodeId?: string,
  destinationTenantId?: string,
  accessibleOnly: boolean = false
): Promise<RouteResponse> {
  const res = await fetch(`${API_BASE}/shopper/navigate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      startNodeId,
      destinationNodeId: destinationNodeId || null,
      destinationTenantId: destinationTenantId || null,
      accessibleOnly,
    }),
  });
  if (!res.ok) throw new Error('Failed to calculate navigation route');
  return res.json();
}

export async function getRouteOverview(
  startNodeId: string,
  destinationNodeId?: string,
  destinationTenantId?: string,
  accessibleOnly: boolean = false
): Promise<RouteOverviewResponse> {
  const res = await fetch(`${API_BASE}/shopper/navigate/overview`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      startNodeId,
      destinationNodeId: destinationNodeId || null,
      destinationTenantId: destinationTenantId || null,
      accessibleOnly,
    }),
  });
  if (!res.ok) throw new Error('Failed to prepare route overview');
  return res.json();
}

export async function recoverLocation(
  clueDescription?: string,
  visibleLandmarks?: string[],
  photoBase64?: string
): Promise<LocationRecoveryResponse> {
  const res = await fetch(`${API_BASE}/shopper/recover-location`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      clueDescription: clueDescription || null,
      visibleLandmarks: visibleLandmarks || null,
      photoBase64: photoBase64 || null,
    }),
  });
  if (!res.ok) throw new Error('Failed to recover location');
  return res.json();
}

export async function extractPoster(
  base64Data?: string,
  mimeType: string = 'image/jpeg'
): Promise<PosterExtractionResultDto> {
  const res = await fetch(`${API_BASE}/tenant/promotions/extract-base64`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({
      base64Data: base64Data || 'AQIDBA==',
      mimeType,
    }),
  });
  if (!res.ok) throw new Error('Failed to extract poster details');
  return res.json();
}

export async function confirmPromotion(
  tenantId: string,
  extractedDetails: PosterExtractionResultDto,
  posterImageUrl?: string
): Promise<PromotionDto> {
  const res = await fetch(`${API_BASE}/tenant/promotions/confirm`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({
      tenantId,
      extractedDetails,
      posterImageUrl: posterImageUrl || null,
    }),
  });
  if (!res.ok) throw new Error('Failed to confirm promotion');
  return res.json();
}

export async function clarifyPromotion(
  promotionId: string,
  details: {
    suitableForGifting?: string;
    suitableForCorporate?: string;
    advanceOrderRequired?: string;
    dietaryInformation?: string;
    approximatePriceRange?: string;
  }
): Promise<EnrichmentClarificationResponse> {
  const res = await fetch(`${API_BASE}/tenant/promotions/clarify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({
      promotionId,
      ...details,
    }),
  });
  if (!res.ok) throw new Error('Failed to clarify promotion');
  return res.json();
}

export async function getPromotions(activeOnly: boolean = true): Promise<PromotionDto[]> {
  const res = await fetch(`${API_BASE}/tenant/promotions?activeOnly=${activeOnly}`);
  return res.json();
}

export async function getIntentSummary(days: number = 7): Promise<IntentAnalyticsSummaryDto> {
  const res = await fetch(`${API_BASE}/mall/bi/summary?days=${days}`);
  return res.json();
}

export async function getBiInsights(days: number = 7): Promise<BiInsightReportDto> {
  const res = await fetch(`${API_BASE}/mall/bi/insights?days=${days}`);
  return res.json();
}

export async function getTodayBiSummary(days: 1 | 7 | 30 = 1): Promise<DailyBiSummaryDto> {
  const res = await fetch(`${API_BASE}/mall/bi/today?days=${days}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load today’s BI summary');
  return res.json();
}

export async function generateTodayBiNarrative(
  days: 1 | 7 | 30 = 1,
  trigger: 'Manual' | 'Scheduled' = 'Manual',
): Promise<BiNarrativeDto> {
  const res = await fetch(`${API_BASE}/mall/bi/today/narrative?days=${days}&trigger=${trigger}`, {
    method: 'POST',
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error('Failed to generate the AI summary');
  return res.json();
}

export async function getLatestBiSummary(days: 1 | 7 | 30 = 1): Promise<BiSummaryHistoryDto | null> {
  const res = await fetch(`${API_BASE}/mall/bi/summaries/latest?days=${days}`, { headers: getAuthHeaders() });
  if (res.status === 204) return null;
  if (!res.ok) throw new Error('Failed to load the latest saved summary');
  return res.json();
}

export async function getBiSummaryHistory(days?: 1 | 7 | 30): Promise<BiSummaryHistoryDto[]> {
  const query = days ? `?days=${days}` : '';
  const res = await fetch(`${API_BASE}/mall/bi/summaries${query}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load summary history');
  return res.json();
}

export async function askTodayBi(
  message: string,
  conversation: BiChatTurnDto[],
  days: 1 | 7 | 30 = 1,
  sessionId?: string,
): Promise<BiChatResponse> {
  const res = await fetch(`${API_BASE}/mall/bi/today/chat?days=${days}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ message, conversation, sessionId }),
  });
  if (!res.ok) throw new Error('The Sunway Kluang Mall assistant could not answer that question');
  return res.json();
}

export async function getBiChatHistory(days: 1 | 7 | 30 = 1): Promise<BiConversationSummaryDto[]> {
  const res = await fetch(`${API_BASE}/mall/bi/chats?days=${days}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load AI conversation history');
  return res.json();
}

export async function getBiChat(sessionId: string): Promise<BiConversationHistoryDto> {
  const res = await fetch(`${API_BASE}/mall/bi/chats/${encodeURIComponent(sessionId)}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load the AI conversation');
  return res.json();
}

export async function trackShopperBehavior(input: ShopperBehaviorEventInput): Promise<void> {
  const res = await fetch(`${API_BASE}/shopper/behavior`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ sessionId: getBehaviorSessionId(), surfaceContext: 'DealsEvents', ...input }),
    keepalive: true,
  });
  if (!res.ok) throw new Error('Failed to record shopper behavior');
}

export async function simulateTraffic(): Promise<{ message: string }> {
  const res = await fetch(`${API_BASE}/mall/bi/simulate-traffic`, { method: 'POST' });
  return res.json();
}

export async function getNavigationNodes(floor?: number): Promise<NavigationNode[]> {
  const url = floor !== undefined ? `${API_BASE}/navigation/nodes?floor=${floor}` : `${API_BASE}/navigation/nodes`;
  const res = await fetch(url, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load navigation nodes');
  return res.json();
}

export async function getNavigationLocations(floor?: number): Promise<NavigationLocation[]> {
  const url = floor !== undefined ? `${API_BASE}/navigation/locations?floor=${floor}` : `${API_BASE}/navigation/locations`;
  const res = await fetch(url);
  if (!res.ok) throw new Error('Failed to load navigation locations');
  return res.json();
}

export async function getNavigationEdges(): Promise<NavigationEdge[]> {
  const res = await fetch(`${API_BASE}/navigation/edges`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load map connections');
  return res.json();
}

export async function saveNavigationNode(input: SaveNavigationNodeInput, id?: string): Promise<NavigationNode> {
  const res = await fetch(`${API_BASE}/navigation/nodes${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify(input),
  });
  if (!res.ok) return readApiError(res, 'Failed to save navigation node');
  return res.json();
}

export async function deleteNavigationNode(id: string): Promise<void> {
  const res = await fetch(`${API_BASE}/navigation/nodes/${id}`, { method: 'DELETE', headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to delete navigation node');
}

export async function saveNavigationEdge(input: SaveNavigationEdgeInput, id?: string): Promise<NavigationEdge> {
  const res = await fetch(`${API_BASE}/navigation/edges${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify(input),
  });
  if (!res.ok) return readApiError(res, 'Failed to save connection');
  return res.json();
}

export async function deleteNavigationEdge(id: string): Promise<void> {
  const res = await fetch(`${API_BASE}/navigation/edges/${id}`, { method: 'DELETE', headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to delete connection');
}

export async function validateNavigationGraph(): Promise<NavigationGraphValidation> {
  const res = await fetch(`${API_BASE}/navigation/validate`, { headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to validate graph');
  return res.json();
}

export async function generateNavigationAiDraft(input: {
  floorNumber: number;
  message: string;
  conversation: NavigationAiTurn[];
  currentNodes: NavigationAiDraft['nodes'];
  currentEdges: NavigationAiDraft['edges'];
  files: File[];
}): Promise<NavigationAiDraft> {
  const form = new FormData();
  form.append('requestJson', JSON.stringify({
    floorNumber: input.floorNumber, message: input.message, conversation: input.conversation,
    currentNodes: input.currentNodes, currentEdges: input.currentEdges,
  }));
  input.files.forEach((file) => form.append('files', file));
  const res = await fetch(`${API_BASE}/navigation/ai-draft`, { method: 'POST', headers: getAuthHeaders(), body: form });
  if (!res.ok) return readApiError(res, 'AI could not prepare a navigation draft');
  return res.json();
}

export async function searchStorefront(query: string, currentFloor?: number, surfaceContext: 'Directory' | 'DealsEvents' = 'Directory'): Promise<StorefrontSearchResponse> {
  const res = await fetch(`${API_BASE}/shopper/search`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ query, currentFloor: currentFloor || null, surfaceContext, sessionId: getBehaviorSessionId() }),
  });
  if (!res.ok) return readApiError(res, 'Storefront search failed');
  return res.json();
}

export async function getAllStores(): Promise<StoreDto[]> {
  const res = await fetch(`${API_BASE}/stores`);
  if (!res.ok) throw new Error('Failed to load tenants');
  return res.json();
}

export async function createStore(
  input: Omit<StoreDto, 'id' | 'navigationNodeId' | 'storefrontImageUrl' | 'isActive'>
): Promise<StoreDto> {
  const res = await fetch(`${API_BASE}/stores`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) return readApiError(res, 'Failed to create tenant');
  return res.json();
}

export async function updateStore(
  storeId: string,
  input: Omit<StoreDto, 'id' | 'navigationNodeId' | 'storefrontImageUrl'>
): Promise<StoreDto> {
  const res = await fetch(`${API_BASE}/stores/${storeId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) return readApiError(res, 'Failed to update tenant');
  return res.json();
}

export async function updateStorefrontImage(storeId: string, storefrontImageUrl: string): Promise<StoreDto> {
  const res = await fetch(`${API_BASE}/stores/${storeId}/storefront-image`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ storefrontImageUrl }),
  });
  if (!res.ok) return readApiError(res, 'Failed to update storefront image');
  return res.json();
}

export async function validateTenantSearch(storeId: string, description: string, generateNewQuestions = false) {
  const res = await fetch(`${API_BASE}/stores/${storeId}/search-validation`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ description, generateNewQuestions }),
  });
  if (!res.ok) return readApiError(res, 'Search validation failed');
  return res.json() as Promise<import('./types').TenantSearchValidationResult>;
}

export async function enrichTenantDescription(storeId: string, description: string) {
  const res = await fetch(`${API_BASE}/stores/${storeId}/description-enrichment`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ description }),
  });
  if (!res.ok) return readApiError(res, 'Description enrichment failed');
  return res.json() as Promise<import('./types').TenantDescriptionEnrichmentResult>;
}

export async function deleteStore(storeId: string): Promise<void> {
  const res = await fetch(`${API_BASE}/stores/${storeId}`, { method: 'DELETE', headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to delete tenant');
}

export async function getUsers(): Promise<ManagedUserDto[]> {
  const res = await fetch(`${API_BASE}/users`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load users');
  return res.json();
}

export async function getUserConversations(userId: string): Promise<ShopperConversationSummaryDto[]> {
  const res = await fetch(`${API_BASE}/users/${userId}/conversations`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error('Failed to load user conversations');
  return res.json();
}

export async function getUserConversation(userId: string, sessionId: string): Promise<ShopperConversationHistoryDto> {
  const res = await fetch(`${API_BASE}/users/${userId}/conversations/${encodeURIComponent(sessionId)}`, { headers: getAuthHeaders() });
  if (!res.ok) throw new Error(res.status === 404 ? 'Conversation not found' : 'Failed to load conversation');
  return res.json();
}

export async function createUser(input: {
  fullName: string;
  email: string;
  password: string;
  role: Exclude<UserRole, 'Guest'>;
  tenantId?: string | null;
}): Promise<ManagedUserDto> {
  const res = await fetch(`${API_BASE}/users`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ ...input, tenantId: input.tenantId || null }),
  });
  if (!res.ok) {
    const payload = await res.json().catch(() => null) as { error?: string } | null;
    throw new Error(payload?.error || 'Failed to create user');
  }
  return res.json();
}

export async function updateUserAccess(
  userId: string,
  role: Exclude<UserRole, 'Guest'>,
  tenantId?: string | null
): Promise<ManagedUserDto> {
  const res = await fetch(`${API_BASE}/users/${userId}/access`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ role, tenantId: tenantId || null }),
  });
  if (!res.ok) {
    const payload = await res.json().catch(() => null) as { error?: string } | null;
    throw new Error(payload?.error || 'Failed to update user access');
  }
  return res.json();
}

async function readApiError(res: Response, fallback: string): Promise<never> {
  const payload = await res.json().catch(() => null) as { error?: string } | null;
  throw new Error(payload?.error || fallback);
}

export async function getKnowledgeCollections(): Promise<KnowledgeCollectionDto[]> {
  const res = await fetch(`${API_BASE}/knowledge/collections`, { headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to load knowledge collections');
  return res.json();
}

export async function getKnowledgeSources(collectionId?: string): Promise<KnowledgeSourceDto[]> {
  const suffix = collectionId ? `?collectionId=${encodeURIComponent(collectionId)}` : '';
  const res = await fetch(`${API_BASE}/knowledge/sources${suffix}`, { headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to load knowledge sources');
  return res.json();
}

export async function getPublishedKnowledgeSources(kind?: string): Promise<PublicKnowledgeSourceDto[]> {
  const suffix = kind ? `?kind=${encodeURIComponent(kind)}` : '';
  const res = await fetch(`${API_BASE}/knowledge/public-sources${suffix}`);
  if (!res.ok) return readApiError(res, 'Failed to load published knowledge');
  return res.json();
}

export async function importKnowledgeText(input: {
  collectionId: string; title: string; sourceKind: string; content?: string; storageReference?: string;
  mimeType?: string; actionMode?: AgentActionMode;
}): Promise<KnowledgeSourceDto> {
  const res = await fetch(`${API_BASE}/knowledge/import`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify({ actionMode: 'AskFirst', ...input }),
  });
  if (!res.ok) return readApiError(res, 'Failed to import knowledge');
  return res.json();
}

export async function importKnowledgeFile(collectionId: string, title: string, file: File, actionMode: AgentActionMode): Promise<KnowledgeSourceDto> {
  const form = new FormData();
  form.append('collectionId', collectionId); form.append('title', title); form.append('sourceKind', file.type.startsWith('image/') ? 'Image' : 'File');
  form.append('actionMode', actionMode); form.append('file', file);
  const res = await fetch(`${API_BASE}/knowledge/import-file`, { method: 'POST', headers: getAuthHeaders(), body: form });
  if (!res.ok) return readApiError(res, 'Failed to import file');
  return res.json();
}

export async function importKnowledgeFiles(collectionId: string, files: File[], actionMode: AgentActionMode = 'AskFirst'): Promise<KnowledgeSourceDto[]> {
  const form = new FormData();
  form.append('collectionId', collectionId); form.append('actionMode', actionMode);
  files.forEach((file) => form.append('files', file));
  const res = await fetch(`${API_BASE}/knowledge/import-files`, { method: 'POST', headers: getAuthHeaders(), body: form });
  if (!res.ok) return readApiError(res, 'Failed to import document batch');
  return res.json();
}

export async function investigateKnowledge(sourceId: string, message: string, actionMode: AgentActionMode): Promise<KnowledgeSourceDto> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/investigate`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify({ message, actionMode }),
  });
  if (!res.ok) return readApiError(res, 'Failed to continue the investigation');
  return res.json();
}

export async function updateKnowledgeSource(sourceId: string, input: {
  title: string; collectionId?: string; sourceKind?: 'General' | 'Deal' | 'Event'; content?: string; storageReference?: string; aiInstruction?: string; actionMode?: AgentActionMode;
}): Promise<KnowledgeSourceDto> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify({ actionMode: 'AskFirst', ...input }),
  });
  if (!res.ok) return readApiError(res, 'Failed to update knowledge');
  return res.json();
}

export async function updateKnowledgeStatus(sourceId: string, status: 'Draft' | 'Published'): Promise<KnowledgeSourceDto> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/status`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify({ status }),
  });
  if (!res.ok) return readApiError(res, 'Failed to update knowledge status');
  return res.json();
}

export async function deleteKnowledgeSource(sourceId: string): Promise<void> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}`, { method: 'DELETE', headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to delete knowledge');
}

export async function createKnowledgeSuggestion(sourceId: string, instruction: string, files: File[]): Promise<KnowledgeSourceDto> {
  const form = new FormData(); form.append('instruction', instruction); files.forEach((file) => form.append('files', file));
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/suggestions`, { method: 'POST', headers: getAuthHeaders(), body: form });
  if (!res.ok) return readApiError(res, 'Failed to create AI suggestion');
  return res.json();
}

export async function createKnowledgeSuggestionWithProgress(
  sourceId: string,
  instruction: string,
  files: File[],
  onProgress: (progress: import('./types').KnowledgeAiProgressDto) => void,
): Promise<KnowledgeSourceDto> {
  const form = new FormData();
  form.append('instruction', instruction);
  files.forEach((file) => form.append('files', file));
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/suggestions/stream`, { method: 'POST', headers: getAuthHeaders(), body: form });
  if (!res.ok) return readApiError(res, 'Failed to create AI suggestion');
  if (!res.body) throw new Error('The AI progress stream was not available.');

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let result: KnowledgeSourceDto | null = null;
  const consume = (line: string) => {
    if (!line.trim()) return;
    const event = JSON.parse(line) as { type: string; progress?: import('./types').KnowledgeAiProgressDto; source?: KnowledgeSourceDto; error?: string };
    if (event.type === 'progress' && event.progress) onProgress(event.progress);
    if (event.type === 'result' && event.source) result = event.source;
    if (event.type === 'error') throw new Error(event.error || 'AI suggestion failed.');
  };

  while (true) {
    const { value, done } = await reader.read();
    buffer += decoder.decode(value, { stream: !done });
    const lines = buffer.split('\n');
    buffer = lines.pop() || '';
    lines.forEach(consume);
    if (done) break;
  }
  consume(buffer);
  if (!result) throw new Error('The AI completed without returning a proposal.');
  return result;
}

export async function acceptKnowledgeSuggestion(sourceId: string, suggestionId: string, actionMode: AgentActionMode): Promise<KnowledgeSourceDto> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/suggestions/${suggestionId}/accept`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify({ actionMode }),
  });
  if (!res.ok) return readApiError(res, 'Failed to accept suggestion');
  return res.json();
}

export async function rejectKnowledgeSuggestion(sourceId: string, suggestionId: string): Promise<KnowledgeSourceDto> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/suggestions/${suggestionId}/reject`, { method: 'POST', headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to reject suggestion');
  return res.json();
}

export async function publishKnowledge(sourceId: string): Promise<KnowledgeSourceDto> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/publish`, { method: 'POST', headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to publish knowledge');
  return res.json();
}

export async function searchKnowledge(query: string, publishedOnly = true, kinds: string[] = []): Promise<KnowledgeSearchResponse> {
  const kindQuery = kinds.map((kind) => `&kinds=${encodeURIComponent(kind)}`).join('');
  const res = await fetch(`${API_BASE}/knowledge/search?query=${encodeURIComponent(query)}&publishedOnly=${publishedOnly}${kindQuery}`, { headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Knowledge search failed');
  return res.json();
}

export async function generateFloorPlanDraft(sourceId: string, floorNumber: number, floorName: string, actionMode: AgentActionMode = 'AskFirst'): Promise<FloorPlanDraftDto> {
  const res = await fetch(`${API_BASE}/knowledge/sources/${sourceId}/floor-plan-draft`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }, body: JSON.stringify({ floorNumber, floorName, actionMode }),
  });
  if (!res.ok) return readApiError(res, 'Failed to generate floor-plan graph');
  return res.json();
}

export async function publishFloorPlanDraft(draftId: string): Promise<FloorPlanDraftDto> {
  const res = await fetch(`${API_BASE}/knowledge/floor-plan-drafts/${draftId}/publish`, { method: 'POST', headers: getAuthHeaders() });
  if (!res.ok) return readApiError(res, 'Failed to publish navigation graph');
  return res.json();
}

export async function updateFloorPlanDraft(draft: FloorPlanDraftDto): Promise<FloorPlanDraftDto> {
  const res = await fetch(`${API_BASE}/knowledge/floor-plan-drafts/${draft.id}`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({ nodes: draft.nodes, edges: draft.edges, warnings: draft.warnings }),
  });
  if (!res.ok) return readApiError(res, 'Failed to validate and save the graph draft');
  return res.json();
}
