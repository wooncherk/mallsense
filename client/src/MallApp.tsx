import { useEffect, useState } from 'react';
import {
  AlertTriangle,
  Bot,
  Clock3,
  LayoutDashboard,
  LogIn,
  LogOut,
  MapPin,
  Home,
  ShoppingBag,
  Tag,
} from 'lucide-react';
import { checkHealth, clearAuthSession, createGuestSession, getCurrentUser, getNavigationLocations } from './api';
import { AdminArea, type AdminSection } from './components/AdminArea';
import { ConciergeWidget } from './components/ConciergeWidget';
import { DirectoryView } from './components/DirectoryView';
import { HomeView } from './components/HomeView';
import { LoginPage } from './components/LoginPage';
import { DealsEventsView } from './components/DealsEventsView';
import { ShopperView } from './components/ShopperView';
import type { NavigationLocation, StoreDto, UserProfileDto } from './types';
import { mallConfig } from './mallConfig';

type ShopperTab = 'home' | 'directory' | 'offers' | 'ai' | 'wayfinding';
const COPYRIGHT_YEAR = new Date().getFullYear();

const shopperPaths: Record<ShopperTab, string> = {
  home: '/',
  directory: '/directory',
  offers: '/deals',
  ai: '/ai',
  wayfinding: '/directions',
};

const adminPaths: Record<AdminSection, string> = {
  overview: '/admin/overview',
  bi: '/admin/business-intelligence',
  knowledge: '/admin/knowledge',
  map: '/admin/mall-map',
  tenants: '/admin/tenants',
  users: '/admin/users',
};

function adminSectionForRoute(route: string): AdminSection {
  if (route === adminPaths.bi) return 'bi';
  if (route === adminPaths.knowledge) return 'knowledge';
  if (route === adminPaths.map) return 'map';
  if (route === adminPaths.tenants) return 'tenants';
  if (route === adminPaths.users) return 'users';
  return 'overview';
}

function shopperTabForRoute(route: string): ShopperTab {
  if (route === '/directory') return 'directory';
  if (route === '/deals') return 'offers';
  if (route === '/ai') return 'ai';
  if (route === '/directions') return 'wayfinding';
  return 'home';
}

const shopperTabs: Array<{ id: ShopperTab; label: string; mobileLabel: string; icon: typeof ShoppingBag }> = [
  { id: 'home', label: 'Home', mobileLabel: 'Home', icon: Home },
  { id: 'directory', label: 'Directory', mobileLabel: 'Directory', icon: ShoppingBag },
  { id: 'offers', label: 'Deals & events', mobileLabel: 'Deals', icon: Tag },
  { id: 'ai', label: 'Ask AI', mobileLabel: 'Ask AI', icon: Bot },
];

export function MallApp() {
  const [route, setRoute] = useState(window.location.pathname);
  const [profile, setProfile] = useState<UserProfileDto | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);
  const [apiOnline, setApiOnline] = useState<boolean | null>(null);
  const [nodes, setNodes] = useState<NavigationLocation[]>([]);
  const [selectedDestinationNodeId, setSelectedDestinationNodeId] = useState<string | undefined>(() => new URLSearchParams(window.location.search).get('destination') ?? undefined);
  const [conciergeQuery, setConciergeQuery] = useState<string>();
  const [askAiPageQuery, setAskAiPageQuery] = useState<string>();
  const [conciergeOpen, setConciergeOpen] = useState(false);
  const activeTab = shopperTabForRoute(route);

  useEffect(() => {
    checkHealth().then(() => setApiOnline(true)).catch(() => setApiOnline(false));
    getNavigationLocations().then(setNodes).catch(() => setNodes([]));
    const restoreIdentity = async () => {
      try {
        setProfile(await getCurrentUser());
      } catch {
        clearAuthSession();
        try {
          await createGuestSession();
          setProfile(await getCurrentUser());
        } catch {
          setProfile(null);
        }
      } finally {
        setCheckingSession(false);
      }
    };
    void restoreIdentity();
  }, []);

  useEffect(() => {
    const handlePopState = () => {
      setRoute(window.location.pathname);
      setSelectedDestinationNodeId(new URLSearchParams(window.location.search).get('destination') ?? undefined);
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  const navigate = (path: string, replace = false) => {
    if (replace) window.history.replaceState({}, '', path);
    else window.history.pushState({}, '', path);
    setRoute(new URL(path, window.location.origin).pathname);
    window.scrollTo({ top: 0 });
  };

  const selectShopperTab = (tab: ShopperTab) => {
    if (tab === 'wayfinding') setSelectedDestinationNodeId(undefined);
    if (tab === 'ai') setAskAiPageQuery(undefined);
    navigate(shopperPaths[tab]);
  };

  const handleNavigateToStore = async (store: StoreDto) => {
    const availableNodes = nodes.length ? nodes : await getNavigationLocations().catch(() => []);
    if (!nodes.length && availableNodes.length) setNodes(availableNodes);
    const normalizedStoreName = store.name.trim().toLowerCase();
    const matchingNode = availableNodes.find((node) => node.id === store.navigationNodeId)
      ?? availableNodes.find((node) => node.tenantId === store.id)
      ?? availableNodes.find((node) => node.label.trim().toLowerCase() === normalizedStoreName);
    const destinationId = matchingNode?.id;
    setSelectedDestinationNodeId(destinationId);
    setConciergeOpen(false);
    navigate(destinationId ? `/directions?destination=${encodeURIComponent(destinationId)}` : '/directions');
  };

  const handleAskConcierge = (query: string) => {
    setConciergeQuery(query);
    setConciergeOpen(true);
  };

  const handleOpenAskAi = (query: string) => {
    setAskAiPageQuery(query);
    setConciergeOpen(false);
    navigate(shopperPaths.ai);
  };

  const handleWidgetNavigation = (nodeId: string, destinationName: string) => {
    setSelectedDestinationNodeId(nodeId);
    setConciergeOpen(false);
    navigate(`/directions?destination=${encodeURIComponent(nodeId)}`);
    document.title = `${destinationName} directions · ${mallConfig.platformName}`;
  };

  const signOut = () => {
    clearAuthSession();
    setProfile(null);
    navigate('/', true);
    void createGuestSession()
      .then(() => getCurrentUser())
      .then(setProfile)
      .catch(() => undefined);
  };

  const handleAuthenticated = (authenticatedProfile: UserProfileDto) => {
    setProfile(authenticatedProfile);
    const returnUrl = new URLSearchParams(window.location.search).get('returnUrl');
    const canAdminister = authenticatedProfile.role === 'Admin' || authenticatedProfile.role === 'TenantStaff';
    navigate(returnUrl?.startsWith('/admin') && canAdminister ? returnUrl : '/', true);
  };

  const canAdminister = profile?.role === 'Admin' || profile?.role === 'TenantStaff';
  const hasRegisteredAccount = profile != null && profile.role !== 'Guest';

  useEffect(() => {
    if (!route.startsWith('/admin') || checkingSession) return;
    if (!profile) {
      navigate(`/login?returnUrl=${encodeURIComponent(route)}`, true);
    } else if (!canAdminister) {
      window.location.replace('/');
    } else if (route === '/admin') {
      navigate(adminPaths.overview, true);
    } else if (!Object.values(adminPaths).includes(route)) {
      navigate(adminPaths.overview, true);
    } else if (profile.role !== 'Admin' && ['map', 'tenants', 'users'].includes(adminSectionForRoute(route))) {
      navigate(adminPaths.overview, true);
    }
  }, [canAdminister, checkingSession, profile, route]);

  if (route === '/login') {
    return <LoginPage onAuthenticated={handleAuthenticated} onBack={() => navigate('/')} />;
  }

  if (route.startsWith('/admin')) {
    if (checkingSession) return <div data-theme="light" className="grid min-h-screen place-items-center bg-base-200"><span className="loading loading-spinner loading-md" /></div>;
    if (!profile) {
      return <LoginPage onAuthenticated={handleAuthenticated} onBack={() => navigate('/')} />;
    }
    if (!canAdminister) {
      return <div data-theme="light" className="grid min-h-screen place-items-center bg-base-200"><span className="loading loading-spinner loading-md" /></div>;
    }
    return <AdminArea profile={profile} section={adminSectionForRoute(route)} onSectionChange={(section) => navigate(adminPaths[section])} onExit={() => navigate('/')} onSignOut={signOut} />;
  }

  return (
    <div data-theme="light" className="mall-shell min-h-screen text-base-content">
      {canAdminister && (
        <div className="bg-neutral px-4 py-2 text-xs text-neutral-content md:px-8">
          <div className="mx-auto flex max-w-7xl items-center justify-end">
            <a className="inline-flex items-center gap-1.5 font-semibold hover:text-white" href={adminPaths.overview}><LayoutDashboard size={13} /> Administration</a>
          </div>
        </div>
      )}
      <div className="border-b border-base-300 bg-base-100 px-4 py-2 text-xs text-base-content/60 md:px-8">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-x-5 gap-y-1">
            <span className="inline-flex items-center gap-1.5"><Clock3 size={13} /> Open {mallConfig.openingHours}</span>
            <span className="hidden items-center gap-1.5 sm:inline-flex"><MapPin size={13} /> {mallConfig.locationLabel}</span>
          </div>
          <div className="flex items-center gap-4">
            {hasRegisteredAccount ? <button className="inline-flex items-center gap-1 hover:text-base-content" onClick={signOut}><LogOut size={13} /> Log out</button> : <button className="inline-flex items-center gap-1 font-medium hover:text-base-content" onClick={() => navigate('/login')}><LogIn size={13} /> Log in / Register</button>}
          </div>
        </div>
      </div>

      <header className="mall-header-shadow sticky top-0 z-40 border-b border-base-300/80 bg-base-100/95 backdrop-blur-xl">
        <div className="navbar relative mx-auto min-h-[4.75rem] max-w-7xl px-4 md:px-8">
          <button className="navbar-start w-auto gap-3 text-left" onClick={() => selectShopperTab('home')}>
            <span className="grid size-10 place-items-center rounded-xl bg-primary text-primary-content shadow-sm"><Bot size={19} /></span>
            <span>
              <span className="block text-lg font-extrabold tracking-[-0.04em]">{mallConfig.platformName}</span>
            </span>
          </button>

          <nav className="absolute left-1/2 hidden -translate-x-1/2 lg:flex" aria-label="Shopper navigation">
            <div className="flex items-center gap-8">
              {shopperTabs.map(({ id, label, icon: Icon }) => (
                <a
                  key={id}
                   href={shopperPaths[id]}
                  className={`inline-flex items-center gap-1.5 border-b-2 py-2 text-sm font-medium transition-colors ${activeTab === id ? 'border-primary text-base-content' : 'border-transparent text-base-content/55 hover:text-base-content'}`}
                  aria-current={activeTab === id ? 'page' : undefined}
                  onClick={(event) => {
                    event.preventDefault();
                    selectShopperTab(id);
                  }}
                >
                  <Icon size={14} /> {label}
                </a>
              ))}
            </div>
          </nav>

          <div className="navbar-end ml-auto w-auto" />
        </div>

        <nav className="grid grid-cols-4 border-t border-base-200 px-2 lg:hidden" aria-label="Mobile shopper navigation">
          {shopperTabs.map(({ id, mobileLabel, icon: Icon }) => (
            <a
              key={id}
               href={shopperPaths[id]}
              className={`inline-flex min-w-0 items-center justify-center gap-1 border-b-2 px-1 py-3 text-xs transition-colors ${activeTab === id ? 'border-primary text-base-content' : 'border-transparent text-base-content/55 hover:text-base-content'}`}
              aria-current={activeTab === id ? 'page' : undefined}
              onClick={(event) => {
                event.preventDefault();
                selectShopperTab(id);
              }}
            >
              <Icon className="shrink-0" size={13} /> <span className="truncate">{mobileLabel}</span>
            </a>
          ))}
        </nav>
      </header>

      {apiOnline === false && (
        <div className="alert alert-error mx-auto mt-5 max-w-7xl rounded-lg px-4" role="alert">
          <AlertTriangle size={18} /> Some live mall information may be unavailable.
        </div>
      )}

      <main className="mx-auto min-h-[55vh] w-full max-w-7xl px-4 py-10 md:px-8 md:py-14">
        {activeTab === 'home' && <HomeView onDirectory={() => selectShopperTab('directory')} onOffers={() => selectShopperTab('offers')} onDirections={() => selectShopperTab('wayfinding')} onAsk={(query) => { setConciergeQuery(query); setConciergeOpen(true); }} />}
        {activeTab === 'directory' && <DirectoryView onNavigateToStore={handleNavigateToStore} onAskConcierge={handleAskConcierge} onAskAiPage={handleOpenAskAi} />}
        {activeTab === 'offers' && <DealsEventsView onNavigateToStore={handleNavigateToStore} onAskConcierge={handleAskConcierge} onAskAiPage={handleOpenAskAi} />}
        {activeTab === 'ai' && <ShopperView
          userId={profile?.id}
          initialTab="chat"
          initialQuery={askAiPageQuery}
          chatOnly
          onNavigateToNode={handleWidgetNavigation}
          onOpenDirectory={() => selectShopperTab('directory')}
          onOpenDeals={() => selectShopperTab('offers')}
          onOpenDirections={() => selectShopperTab('wayfinding')}
        />}
        {activeTab === 'wayfinding' && <ShopperView userId={profile?.id} initialTab="navigate" initialDestinationId={selectedDestinationNodeId} navigationOnly />}
      </main>

      {activeTab !== 'ai' && activeTab !== 'wayfinding' && <ConciergeWidget userId={profile?.id} open={conciergeOpen} initialQuery={conciergeQuery} onOpen={() => { setConciergeQuery(undefined); setConciergeOpen(true); }} onClose={() => setConciergeOpen(false)} onOpenPage={() => { setConciergeOpen(false); selectShopperTab('ai'); }} onNavigate={handleWidgetNavigation} />}

      <footer className="border-t border-base-300 bg-base-100 px-4 py-7 text-sm text-base-content/55 md:px-8">
        <div className="mx-auto flex max-w-7xl justify-center">
          <span className="text-center">© {COPYRIGHT_YEAR} {mallConfig.copyrightHolder}</span>
        </div>
      </footer>
    </div>
  );
}
