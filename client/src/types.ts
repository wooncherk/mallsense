export interface StructuredIntentDto {
  intent: string;
  recipient: string | null;
  categoryInterests: string[];
  excludedCategories: string[];
  preferences: string[];
  budgetSignal: string | null;
  resultFocus: 'Stores' | 'DealsEvents' | 'Mixed';
  needsMallSearch?: boolean;
  searchQuery?: string | null;
}

export interface RecommendationCardDto {
  tenantId: string;
  tenantName: string;
  floorNumber: number;
  unitNumber: string;
  category: number;
  description: string;
  whyRecommended: string;
  activePromotionTitle: string | null;
  activePromotionOffer: string | null;
  navigationNodeId: string | null;
}

export interface ShopperChatResponse {
  sessionId: string;
  reply: string;
  structuredIntent: StructuredIntentDto;
  recommendations: RecommendationCardDto[];
  suggestedFollowUps: string[];
  replySuggestions: ShopperReplySuggestionDto[];
}

export interface ShopperReplySuggestionDto {
  label: string;
  prompt: string;
}

export interface ShopperConversationTurnDto {
  role: 'user' | 'assistant';
  message: string;
}

export interface ShopperConversationHistoryTurnDto {
  id: string;
  role: 'user' | 'assistant';
  message: string;
  createdAtUtc: string;
  structuredIntent: StructuredIntentDto | null;
  recommendations: RecommendationCardDto[];
  replySuggestions: ShopperReplySuggestionDto[];
}

export interface ShopperConversationHistoryDto {
  sessionId: string;
  surfaceContext: string;
  turns: ShopperConversationHistoryTurnDto[];
  latestIntent: StructuredIntentDto | null;
}

export interface ShopperConversationSummaryDto {
  sessionId: string;
  title: string;
  surfaceContext: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  turnCount: number;
  lastMessage: string;
}

export interface RouteStepDto {
  fromNodeId: string;
  fromNodeLabel: string;
  toNodeId: string;
  toNodeLabel: string;
  distanceMeters: number;
  directionHint: string;
  floorChange: number;
  currentFloor: number;
  instruction: string;
}

export interface RouteResponse {
  found: boolean;
  totalDistanceMeters: number;
  floorChanges: number;
  conversationalDirections: string;
  steps: RouteStepDto[];
  pathNodeIds: string[];
}

export interface RouteOverviewResponse {
  found: boolean;
  conversationalDirections: string;
}

export interface LocationRecoveryResponse {
  estimatedNodeId: string | null;
  estimatedLocationLabel: string;
  estimatedFloor: number | null;
  confidencePercentage: number;
  detectedClues: string[];
  confirmationQuestion: string;
}

export interface PosterExtractionResultDto {
  title: string;
  type: number;
  offerSummary: string;
  validFrom: string;
  validTo: string;
  targetAudience: string;
  occasions: string;
  dietaryFlags: string | null;
  priceRange: string | null;
  suggestedCategories: string[];
  missingInformationQuestions: string[];
}

export interface PromotionDto {
  id: string;
  tenantId: string;
  tenantName: string;
  title: string;
  type: number;
  offerSummary: string;
  validFrom: string;
  validTo: string;
  targetAudience: string;
  occasions: string;
  dietaryFlags: string | null;
  priceRange: string | null;
  advanceOrderRequired: string | null;
  posterImageUrl: string | null;
  readinessScore: number;
  readinessNotes: string;
  isActive: boolean;
}

export interface EnrichmentClarificationResponse {
  promotionId: string;
  newReadinessScore: number;
  readinessFeedback: string;
  updatedPromotion: PromotionDto;
}

export interface IntentStatItemDto {
  label: string;
  count: number;
  percentage: number;
}

export interface IntentAnalyticsSummaryDto {
  totalInteractions: number;
  periodDays: number;
  topIntentCategories: IntentStatItemDto[];
  topCategoryInterests: IntentStatItemDto[];
  topRecipientTypes: IntentStatItemDto[];
  emergingTrends: string[];
}

export interface DemandCampaignMatchDto {
  shopperDemandTrend: string;
  demandIncreasePercentage: number;
  matchingTenantPromotions: string[];
  recommendedAction: string;
}

export interface BiInsightReportDto {
  executiveSummary: string;
  keyPatterns: string[];
  demandMatches: DemandCampaignMatchDto[];
  strategicRecommendations: string[];
  generatedAtUtc: string;
}

export interface DailyBiMetricDto {
  label: string;
  count: number;
}

export interface PotentialBiActionDto {
  title: string;
  rationale: string;
  affectedArea: string;
  expectedImpact: string;
  priority: 'High' | 'Medium' | 'Low';
  requiresApproval: boolean;
}

export interface DailyBiSummaryDto {
  scope: 'Mall' | 'Tenant';
  scopeName: string;
  localDate: string;
  generatedAtUtc: string;
  totalSearches: number;
  uniqueSessions: number;
  dealSearches: number;
  eventSearches: number;
  zeroResultSearches: number;
  contentOpens: number;
  askAiContinuations: number;
  directionRequests: number;
  engagementRate: number;
  topQueries: DailyBiMetricDto[];
  topContent: DailyBiMetricDto[];
  highlights: string[];
  potentialActions: PotentialBiActionDto[];
}

export interface BiNarrativeDto {
  id: string;
  scope: 'Mall' | 'Tenant';
  scopeName: string;
  localDate: string;
  summaryText: string;
  generatedAtUtc: string;
  trigger: 'Manual' | 'Scheduled';
}

export interface BiSummaryHistoryDto {
  id: string;
  scope: 'Mall' | 'Tenant';
  scopeName: string;
  days: 1 | 7 | 30;
  periodLabel: string;
  summaryText: string;
  generatedAtUtc: string;
  trigger: 'Manual' | 'Scheduled';
}

export interface BiChatTurnDto {
  role: 'user' | 'assistant';
  message: string;
}

export interface BiChatResponse {
  answer: string;
  generatedAtUtc: string;
  sessionId: string;
}

export interface BiConversationSummaryDto {
  sessionId: string;
  title: string;
  days: 1 | 7 | 30;
  createdAtUtc: string;
  updatedAtUtc: string;
  turnCount: number;
  lastMessage: string;
}

export interface BiConversationHistoryTurnDto {
  id: string;
  role: 'user' | 'assistant';
  message: string;
  createdAtUtc: string;
}

export interface BiConversationHistoryDto {
  sessionId: string;
  days: 1 | 7 | 30;
  turns: BiConversationHistoryTurnDto[];
}

export interface ShopperBehaviorEventInput {
  eventType: 'SearchResultsViewed' | 'FilterChanged' | 'ContentOpened' | 'AskAiContinued' | 'DirectionsRequested';
  surfaceContext?: 'Directory' | 'DealsEvents' | 'AskAI';
  query?: string;
  selectedFilter?: string;
  resultCount?: number;
  targetKnowledgeSourceId?: string;
  targetTenantId?: string;
  targetTitle?: string;
  relatedTenantIds?: string[];
  relatedKnowledgeSourceIds?: string[];
}

export interface NavigationNode {
  id: string;
  floorNumber: number;
  label: string;
  nodeType: number;
  coordinateX: number;
  coordinateY: number;
  description: string | null;
  tenantId: string | null;
}

export interface NavigationLocation {
  id: string;
  floorNumber: number;
  label: string;
  nodeType: number;
  description: string | null;
  tenantId: string | null;
}

export interface NavigationGraphValidation {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface NavigationAiTurn { role: 'user' | 'assistant'; message: string; }
export interface NavigationAiDraftNode { key: string; floorNumber: number; label: string; type: string; x: number; y: number; description: string | null; confidence: number; }
export interface NavigationAiDraftEdge { from: string; to: string; distanceMeters: number; accessible: boolean; bidirectional: boolean; directionHint: string; }
export interface NavigationAiDraft {
  assistantMessage: string;
  clarificationQuestions: string[];
  nodes: NavigationAiDraftNode[];
  edges: NavigationAiDraftEdge[];
  warnings: string[];
  confidencePercentage: number;
}

export interface SaveNavigationNodeInput {
  floorNumber: number;
  label: string;
  nodeType: number;
  coordinateX: number;
  coordinateY: number;
  description: string | null;
  tenantId: string | null;
}

export interface SaveNavigationEdgeInput {
  sourceNodeId: string;
  targetNodeId: string;
  distanceMeters: number;
  directionHint: string;
  floorChange: number;
  isAccessible: boolean;
  isBidirectional: boolean;
}

export interface NavigationEdge {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
  distanceMeters: number;
  directionHint: string;
  floorChange: number;
  isAccessible: boolean;
  isBidirectional: boolean;
}

export interface StorefrontSearchResponse {
  query: string;
  answer: string;
  sessionId: string;
  structuredIntent: StructuredIntentDto;
  resultFocus: 'Stores' | 'DealsEvents' | 'Mixed';
  stores: RecommendationCardDto[];
  knowledge: KnowledgeSearchResultDto[];
  suggestedFollowUps: string[];
}

export interface StoreDto {
  id: string;
  name: string;
  description: string;
  category: number;
  floorNumber: number;
  unitNumber: string;
  contactEmail: string | null;
  contactPhone: string | null;
  openingHours: string | null;
  storefrontImageUrl: string | null;
  isActive: boolean;
  navigationNodeId: string | null;
}

export type UserRole = 'Guest' | 'Shopper' | 'TenantStaff' | 'Admin';

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  userId: string;
  userName: string;
  role: UserRole;
  tenantId: string | null;
  tenantName: string | null;
}

export interface UserProfileDto {
  id: string;
  userName: string;
  email: string | null;
  fullName: string;
  role: UserRole;
  tenantId: string | null;
  tenantName: string | null;
  createdAtUtc: string;
}

export interface ManagedUserDto extends UserProfileDto {
  lastActiveAtUtc: string;
}

export type AgentActionMode = 'Explore' | 'Suggest' | 'AskFirst' | 'AutoWithinScope';

export interface KnowledgeCollectionDto {
  id: string;
  name: string;
  description: string;
  scopeType: string;
  scopeReferenceId: string | null;
  visibility: string;
  allowedActions: string[];
}

export interface KnowledgeTurnDto {
  sequence: number;
  role: string;
  message: string;
  createdAtUtc: string;
}

export interface KnowledgeSourceDto {
  id: string;
  collectionId: string;
  collectionName: string;
  title: string;
  sourceKind: string;
  mimeType: string;
  storageReference: string | null;
  content: string;
  aiSummary: string;
  suggestedQuestions: string[];
  claims: string[];
  status: string;
  isSimulated: boolean;
  modelName: string | null;
  promptVersion: string;
  createdAtUtc: string;
  conversation: KnowledgeTurnDto[];
  suggestions: KnowledgeSuggestionDto[];
  attachments: KnowledgeAttachmentDto[];
}

export interface KnowledgeSuggestionDto {
  id: string;
  instruction: string;
  baseMarkdown: string;
  proposedMarkdown: string;
  rationale: string;
  changes: string[];
  evidenceNames: string[];
  status: 'Pending' | 'Accepted' | 'Rejected';
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decidedBy: string | null;
}

export interface KnowledgeAiProgressDto {
  stage: string;
  message: string;
  percent: number;
}

export interface KnowledgeAttachmentDto { id: string; fileName: string; mimeType: string; createdAtUtc: string; }

export interface KnowledgeSearchResultDto {
  sourceId: string;
  title: string;
  snippet: string;
  collectionName: string;
  score: number;
  citation: string;
}

export interface KnowledgeSearchResponse {
  query: string;
  results: KnowledgeSearchResultDto[];
  answer: string;
}

export interface PublicKnowledgeSourceDto {
  id: string;
  title: string;
  sourceKind: string;
  markdownContent: string;
  collectionName: string;
  updatedAtUtc: string;
}

export interface FloorPlanNodeDto { key: string; label: string; type: string; x: number; y: number; description: string | null; confidence: number; }
export interface FloorPlanEdgeDto { from: string; to: string; distanceMeters: number; accessible: boolean; bidirectional: boolean; directionHint: string; }
export interface FloorPlanDraftDto {
  id: string;
  sourceId: string;
  floorNumber: number;
  floorName: string;
  nodes: FloorPlanNodeDto[];
  edges: FloorPlanEdgeDto[];
  warnings: string[];
  confidencePercentage: number;
  status: string;
}
