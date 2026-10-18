import React, { useCallback, useState, useEffect } from 'react';
import {
  TrendingUp,
  Sparkles,
  Zap,
  Target,
  Lightbulb,
  ArrowUpRight,
  RefreshCw,
  Layers,
} from 'lucide-react';
import {
  getIntentSummary,
  getBiInsights,
  simulateTraffic,
} from '../api';
import type {
  IntentAnalyticsSummaryDto,
  BiInsightReportDto,
} from '../types';

export const MallView: React.FC = () => {
  const [summary, setSummary] = useState<IntentAnalyticsSummaryDto | null>(null);
  const [insights, setInsights] = useState<BiInsightReportDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [simulating, setSimulating] = useState(false);
  const [simulatedCount, setSimulatedCount] = useState<number | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [sumData, insData] = await Promise.all([
        getIntentSummary(7),
        getBiInsights(7),
      ]);
      setSummary(sumData);
      setInsights(insData);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleSimulateSurge = async () => {
    setSimulating(true);
    try {
      await simulateTraffic();
      setSimulatedCount(15);
      await loadData();
    } catch {
      alert('Error simulating traffic');
    } finally {
      setSimulating(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '28px' }}>
      {/* Header & Simulation Control */}
      <div className="minimal-card" style={{ padding: '24px 28px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <span className="mono-tag" style={{ color: 'var(--text-muted)' }}>Operations & Leasing Intelligence</span>
          <h2 style={{ fontSize: '24px', fontWeight: 800, letterSpacing: '-0.02em', color: 'var(--text-primary)', marginTop: '4px' }}>
            Shopper Intent & Retail Analytics
          </h2>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', marginTop: '4px' }}>
            Transforming conversational interactions into business intelligence for marketing and tenant alignment.
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button
            onClick={loadData}
            disabled={loading}
            className="btn-minimal-secondary"
            style={{ padding: '8px 14px' }}
          >
            <RefreshCw size={14} className={loading ? 'spin' : ''} /> Refresh
          </button>

          <button
            onClick={handleSimulateSurge}
            disabled={simulating}
            className="btn-minimal-primary"
            style={{ padding: '8px 16px' }}
          >
            <Zap size={15} /> {simulating ? 'Injecting Signals...' : 'Simulate Weekend Intent Surge'}
          </button>
        </div>
      </div>

      {simulatedCount && (
        <div style={{ background: '#ecfdf5', border: '1px solid #a7f3d0', padding: '12px 18px', borderRadius: 'var(--radius-sm)', color: '#065f46', fontSize: '13px' }}>
          ✓ Injected {simulatedCount} synthetic customer intent signals. Demand patterns and strategic insights updated.
        </div>
      )}

      {/* Metric Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px' }}>
          <div className="minimal-card" style={{ padding: '22px' }}>
            <div className="mono-tag" style={{ color: 'var(--text-muted)' }}>
              Discovery Interactions
            </div>
            <div style={{ fontSize: '32px', fontWeight: 800, color: 'var(--text-primary)', marginTop: '6px' }}>
              {summary.totalInteractions}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Past {summary.periodDays} days inquiry volume
            </div>
          </div>

          <div className="minimal-card" style={{ padding: '22px' }}>
            <div className="mono-tag" style={{ color: 'var(--text-muted)' }}>
              Top Shopper Category
            </div>
            <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-primary)', marginTop: '6px' }}>
              {summary.topIntentCategories[0]?.label || 'Dining'}
            </div>
            <div style={{ fontSize: '12px', color: '#059669', marginTop: '4px', fontWeight: 600 }}>
              {summary.topIntentCategories[0]?.percentage || 35}% share of voice
            </div>
          </div>

          <div className="minimal-card" style={{ padding: '22px', borderLeft: '3px solid #d97706' }}>
            <div className="mono-tag" style={{ color: '#d97706', display: 'flex', alignItems: 'center', gap: '4px' }}>
              <TrendingUp size={13} /> Emerging Trend
            </div>
            <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-primary)', marginTop: '6px' }}>
              Family & Kids +31%
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Concentrated between 1:00 PM – 5:00 PM
            </div>
          </div>

          <div className="minimal-card" style={{ padding: '22px' }}>
            <div className="mono-tag" style={{ color: 'var(--text-muted)' }}>
              Active Campaign Matches
            </div>
            <div style={{ fontSize: '32px', fontWeight: 800, color: '#4f46e5', marginTop: '6px' }}>
              {insights?.demandMatches.length || 2}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Ready for immediate marketing spotlight
            </div>
          </div>
        </div>
      )}

      {/* Main Grid: GenAI Summary & Demand Matcher */}
      <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 0.8fr', gap: '24px' }}>
        {/* Left Column: GenAI Executive Summary & Demand Matcher */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
          {/* Executive Summary */}
          <div className="minimal-card" style={{ padding: '28px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <h3 style={{ fontSize: '16px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Sparkles size={18} color="#4f46e5" /> GenAI Management Briefing
              </h3>
              <span className="mono-tag" style={{ background: '#e0e7ff', color: '#3730a3', padding: '3px 8px', borderRadius: '4px' }}>
                Synthesized by Gemini
              </span>
            </div>

            <p style={{ fontSize: '14px', color: 'var(--text-primary)', lineHeight: '1.6', background: 'var(--bg-secondary)', padding: '18px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-subtle)' }}>
              {insights?.executiveSummary || 'Loading briefing...'}
            </p>

            <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Observed Behavioral Signals:
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {insights?.keyPatterns.map((pat, idx) => (
                <div key={idx} style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-primary)' }}>
                  <ArrowUpRight size={14} color="var(--text-muted)" /> {pat}
                </div>
              ))}
            </div>
          </div>

          {/* Connected Demand Loop */}
          <div className="minimal-card" style={{ padding: '28px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Target size={18} color="#059669" /> Closing the Loop: Customer Demand ↔ Tenant Campaigns
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Identifies active campaigns uploaded by tenants that directly answer surging visitor requests:
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              {insights?.demandMatches.map((match, idx) => (
                <div key={idx} style={{ background: '#ffffff', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-sm)', padding: '18px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-primary)' }}>
                      {match.shopperDemandTrend}
                    </span>
                    <span style={{ fontSize: '12px', background: '#ecfdf5', color: '#065f46', padding: '2px 8px', borderRadius: '4px', fontWeight: 700 }}>
                      +{match.demandIncreasePercentage}% Surge
                    </span>
                  </div>

                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                    Matching Active Tenant Campaigns:
                  </div>
                  <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                    {match.matchingTenantPromotions.map((p, i) => (
                      <span key={i} style={{ background: '#f4f4f5', border: '1px solid var(--border-subtle)', color: 'var(--text-primary)', padding: '4px 10px', borderRadius: '6px', fontSize: '12px', fontWeight: 500 }}>
                        🏷️ {p}
                      </span>
                    ))}
                  </div>

                  <div style={{ background: '#eff6ff', borderLeft: '3px solid #2563eb', padding: '10px 14px', borderRadius: '4px', fontSize: '13px', color: '#1e40af' }}>
                    <strong>Recommended Action:</strong> {match.recommendedAction}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Right Column: Strategic Actions & Category Split */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
          {/* Strategic Actions */}
          <div className="minimal-card" style={{ padding: '28px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <h3 style={{ fontSize: '15px', fontWeight: 700, display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Lightbulb size={18} color="#d97706" /> Management Recommendations
            </h3>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {insights?.strategicRecommendations.map((rec, idx) => (
                <div key={idx} style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-subtle)', padding: '14px', borderRadius: 'var(--radius-sm)', fontSize: '13px', color: 'var(--text-primary)', lineHeight: '1.5' }}>
                  <span style={{ fontWeight: 700, marginRight: '6px' }}>#{idx + 1}</span>
                  {rec}
                </div>
              ))}
            </div>
          </div>

          {/* Top Categories */}
          {summary && (
            <div className="minimal-card" style={{ padding: '24px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <h3 style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Layers size={16} /> Product / Service Inquiries
              </h3>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                {summary.topCategoryInterests.map((cat, idx) => (
                  <div key={idx}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', marginBottom: '4px' }}>
                      <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{cat.label}</span>
                      <span style={{ color: 'var(--text-muted)' }}>{cat.count} inquiries ({cat.percentage}%)</span>
                    </div>
                    <div style={{ width: '100%', height: '6px', background: '#e4e4e7', borderRadius: '3px', overflow: 'hidden' }}>
                      <div style={{ width: `${cat.percentage}%`, height: '100%', background: 'var(--text-primary)' }} />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
