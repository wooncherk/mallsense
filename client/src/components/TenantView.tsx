import React, { useCallback, useState, useEffect } from 'react';
import {
  UploadCloud,
  CheckCircle2,
  HelpCircle,
  Sparkles,
  ArrowRight,
  TrendingUp,
  Tag,
  Calendar,
  Store as StoreIcon,
  Check,
} from 'lucide-react';
import {
  extractPoster,
  confirmPromotion,
  clarifyPromotion,
  getAllStores,
  getPromotions,
} from '../api';
import type {
  PosterExtractionResultDto,
  PromotionDto,
  StoreDto,
} from '../types';

interface TenantViewProps {
  lockedTenantId?: string;
}

export const TenantView: React.FC<TenantViewProps> = ({ lockedTenantId }) => {
  const [stores, setStores] = useState<StoreDto[]>([]);
  const [selectedTenantId, setSelectedTenantId] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [extractedData, setExtractedData] = useState<PosterExtractionResultDto | null>(null);
  const [confirmedPromo, setConfirmedPromo] = useState<PromotionDto | null>(null);

  // Clarification form inputs
  const [suitableForGifting, setSuitableForGifting] = useState('Personal and Family gifting');
  const [suitableForCorporate, setSuitableForCorporate] = useState('Yes, corporate bulk orders welcome');
  const [advanceOrder, setAdvanceOrder] = useState('Corporate bulk orders (10+ boxes) require 3 days notice');
  const [dietaryInfo, setDietaryInfo] = useState('Halal Certified, Vegetarian Options Available');
  const [approxPrice, setApproxPrice] = useState('RM 88 - RM 198');
  const [readinessScore, setReadinessScore] = useState<number>(0);
  const [clarifyFeedback, setClarifyFeedback] = useState<string>('');

  // Tenant promotions list
  const [tenantPromos, setTenantPromos] = useState<PromotionDto[]>([]);

  const loadPromotions = useCallback(() => {
    getPromotions().then((promotions) => {
      setTenantPromos(lockedTenantId
        ? promotions.filter((promotion) => promotion.tenantId === lockedTenantId)
        : promotions);
    });
  }, [lockedTenantId]);

  useEffect(() => {
    getAllStores().then((data) => {
      setStores(data);
      const popular = data.find((s) => s.id === lockedTenantId) || data.find((s) => s.name.includes('Popular')) || data[0];
      if (popular) setSelectedTenantId(popular.id);
    });
    loadPromotions();
  }, [lockedTenantId, loadPromotions]);

  const handleSimulateUpload = async () => {
    setLoading(true);
    try {
      const result = await extractPoster();
      setExtractedData(result);
      setConfirmedPromo(null);
    } catch {
      alert('Error extracting poster details');
    } finally {
      setLoading(false);
    }
  };

  const handleConfirm = async () => {
    if (!selectedTenantId || !extractedData) return;
    setLoading(true);
    try {
      const created = await confirmPromotion(
        selectedTenantId,
        extractedData,
        'https://mall.example/posters/mid-autumn.jpg'
      );
      setConfirmedPromo(created);
      setReadinessScore(created.readinessScore);
      loadPromotions();
    } catch {
      alert('Error confirming promotion');
    } finally {
      setLoading(false);
    }
  };

  const handleClarify = async () => {
    if (!confirmedPromo) return;
    setLoading(true);
    try {
      const res = await clarifyPromotion(confirmedPromo.id, {
        suitableForGifting,
        suitableForCorporate,
        advanceOrderRequired: advanceOrder,
        dietaryInformation: dietaryInfo,
        approximatePriceRange: approxPrice,
      });

      setReadinessScore(res.newReadinessScore);
      setClarifyFeedback(res.readinessFeedback);
      setConfirmedPromo(res.updatedPromotion);
      loadPromotions();
    } catch {
      alert('Error enriching promotion');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[1.2fr_.8fr]">
      {/* Left Column: Zero-Friction Ingestion & Clarification */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
        {/* Tenant Store Selector */}
        <div className="minimal-card" style={{ padding: '16px 20px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '12px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <StoreIcon size={20} color="var(--text-primary)" />
            <div>
              <div className="mono-tag" style={{ color: 'var(--text-muted)' }}>Tenant Store</div>
              <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-primary)' }}>
                {stores.find((s) => s.id === selectedTenantId)?.name || 'Loading tenant...'}
              </div>
            </div>
          </div>
          <select
            value={selectedTenantId}
            onChange={(e) => setSelectedTenantId(e.target.value)}
            disabled={Boolean(lockedTenantId)}
            style={{
              background: '#ffffff',
              color: 'var(--text-primary)',
              border: '1px solid var(--border-subtle)',
              borderRadius: 'var(--radius-sm)',
              padding: '8px 12px',
              fontSize: '13px',
            }}
          >
            {stores.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name} (Level {s.floorNumber})
              </option>
            ))}
          </select>
        </div>

        {/* Step 1: Upload Poster */}
        <div className="minimal-card" style={{ padding: '28px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '8px' }}>
              <UploadCloud size={18} /> Step 1: Zero-Friction Poster Ingestion
            </h3>
            <span className="mono-tag" style={{ background: '#e0e7ff', color: '#3730a3', padding: '3px 8px', borderRadius: '4px' }}>
              Gemini Vision
            </span>
          </div>
          <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
            Upload existing promotional flyers, posters, or social media artwork. Gemini extracts key fields into structured search data:
          </p>

          <div
            onClick={handleSimulateUpload}
            style={{
              border: '2px dashed var(--border-strong)',
              background: 'var(--bg-secondary)',
              borderRadius: 'var(--radius-md)',
              padding: '36px 20px',
              textAlign: 'center',
              cursor: 'pointer',
              transition: 'all 0.15s ease',
            }}
          >
            <UploadCloud size={32} color="var(--text-muted)" style={{ margin: '0 auto 10px' }} />
            <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-primary)' }}>
              Click to Upload Campaign Poster
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
              JPG, PNG, or PDF • Click to test with sample Mid-Autumn Festival poster
            </div>
          </div>

          {loading && (
            <div style={{ fontSize: '13px', color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Sparkles size={16} /> Gemini Vision is analyzing poster text and layout...
            </div>
          )}

          {/* Extracted Details */}
          {extractedData && (
            <div style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)', padding: '18px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="mono-tag" style={{ color: '#047857', fontWeight: 700 }}>
                  ✓ Extracted Fields
                </span>
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Ready for verification</span>
              </div>

              <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-primary)' }}>
                {extractedData.title}
              </div>

              <div style={{ fontSize: '13px', color: '#92400e', background: '#fef3c7', padding: '8px 12px', borderRadius: '6px' }}>
                <strong>Offer:</strong> {extractedData.offerSummary}
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', fontSize: '12px', color: 'var(--text-muted)' }}>
                <div><strong>Audience:</strong> {extractedData.targetAudience}</div>
                <div><strong>Occasions:</strong> {extractedData.occasions}</div>
                <div><strong>Validity:</strong> {extractedData.validFrom.split('T')[0]} to {extractedData.validTo.split('T')[0]}</div>
                <div><strong>Dietary:</strong> {extractedData.dietaryFlags || 'Not specified'}</div>
              </div>

              {!confirmedPromo && (
                <button
                  onClick={handleConfirm}
                  disabled={loading}
                  className="btn-minimal-primary"
                  style={{ marginTop: '8px' }}
                >
                  <Check size={16} /> Confirm & Save Promotion (Initial Score: 85%)
                </button>
              )}
            </div>
          )}
        </div>

        {/* Step 2: Guided Enrichment */}
        {confirmedPromo && (
          <div className="minimal-card" style={{ padding: '28px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ fontSize: '16px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '8px' }}>
                <HelpCircle size={18} /> Step 2: Conversational Guided Enrichment
              </h3>
              <span className="mono-tag" style={{ background: '#fef3c7', color: '#92400e', padding: '3px 8px', borderRadius: '4px' }}>
                Missing Context
              </span>
            </div>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Supplying additional context helps the Sunway Kluang Mall guide recommend this promotion to relevant shoppers when they ask the Concierge:
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>
                  Is this promotion suitable for personal gifts, family, or corporate gifting?
                </label>
                <input
                  type="text"
                  value={suitableForGifting}
                  onChange={(e) => setSuitableForGifting(e.target.value)}
                  style={{ width: '100%', background: '#ffffff', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>
                  Corporate orders suitability:
                </label>
                <input
                  type="text"
                  value={suitableForCorporate}
                  onChange={(e) => setSuitableForCorporate(e.target.value)}
                  style={{ width: '100%', background: '#ffffff', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>
                  Price range or set pricing:
                </label>
                <input
                  type="text"
                  value={approxPrice}
                  onChange={(e) => setApproxPrice(e.target.value)}
                  style={{ width: '100%', background: '#ffffff', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>
                  Advance notice requirements:
                </label>
                <input
                  type="text"
                  value={advanceOrder}
                  onChange={(e) => setAdvanceOrder(e.target.value)}
                  style={{ width: '100%', background: '#ffffff', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>
                  Dietary notes (Halal, Vegetarian, Gluten-Free):
                </label>
                <input
                  type="text"
                  value={dietaryInfo}
                  onChange={(e) => setDietaryInfo(e.target.value)}
                  style={{ width: '100%', background: '#ffffff', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '8px 12px', fontSize: '13px' }}
                />
              </div>

              <button
                onClick={handleClarify}
                disabled={loading}
                className="btn-minimal-primary"
                style={{ marginTop: '6px' }}
              >
                <ArrowRight size={16} /> Update Context & Maximize Discovery Score
              </button>
            </div>

            {clarifyFeedback && (
              <div style={{ background: '#ecfdf5', border: '1px solid #a7f3d0', padding: '12px', borderRadius: 'var(--radius-sm)', color: '#065f46', fontSize: '13px' }}>
                ✓ {clarifyFeedback}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Right Column: Discovery Readiness Gauge & Active Tenant Campaigns */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
        {/* Discovery Readiness Score Gauge */}
        <div className="minimal-card" style={{ padding: '28px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <h3 style={{ fontSize: '15px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '8px' }}>
              <TrendingUp size={18} color="#059669" /> Discovery Readiness Score
            </h3>
            <span style={{ fontSize: '24px', fontWeight: 800, color: readinessScore >= 90 ? '#059669' : 'var(--text-primary)' }}>
              {readinessScore}%
            </span>
          </div>

          {/* Progress Bar */}
          <div style={{ width: '100%', height: '8px', background: '#e4e4e7', borderRadius: '4px', overflow: 'hidden' }}>
            <div
              style={{
                width: `${readinessScore}%`,
                height: '100%',
                background: readinessScore >= 90 ? '#059669' : 'var(--text-primary)',
                transition: 'width 0.4s ease',
              }}
            />
          </div>

          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Shows how well-equipped the AI companion is to recommend this offer to visiting shoppers:
          </div>

          {/* Minimal Checklist */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '12px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: extractedData ? '#059669' : 'var(--text-subtle)' }}>
              <CheckCircle2 size={15} /> Core offer extracted from artwork
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: confirmedPromo ? '#059669' : 'var(--text-subtle)' }}>
              <CheckCircle2 size={15} /> Campaign validity and headline confirmed
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: readinessScore >= 80 ? '#059669' : 'var(--text-subtle)' }}>
              <CheckCircle2 size={15} /> Target audience & gifting occasions provided
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: readinessScore >= 95 ? '#059669' : 'var(--text-subtle)' }}>
              <CheckCircle2 size={15} /> Dietary, advance booking & price guidance added
            </div>
          </div>
        </div>

        {/* Existing Active Campaigns */}
        <div className="minimal-card" style={{ padding: '24px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <h3 style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Tag size={16} /> Active Tenant Campaigns
          </h3>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', maxHeight: '350px', overflowY: 'auto' }}>
            {tenantPromos.map((p) => (
              <div key={p.id} style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)', padding: '14px', borderRadius: 'var(--radius-sm)', display: 'flex', flexDirection: 'column', gap: '6px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-primary)' }}>{p.title}</div>
                  <span className="mono-tag" style={{ background: '#ffffff', border: '1px solid var(--border-subtle)', padding: '2px 6px', borderRadius: '4px' }}>
                    {p.readinessScore}%
                  </span>
                </div>
                <div style={{ fontSize: '12px', color: '#b45309' }}>{p.offerSummary}</div>
                <div style={{ fontSize: '11px', color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: '4px' }}>
                  <Calendar size={11} /> {p.validFrom.split('T')[0]} to {p.validTo.split('T')[0]} • {p.targetAudience}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
