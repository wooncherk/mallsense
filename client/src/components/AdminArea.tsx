import { lazy, Suspense, useEffect, useMemo, useState } from 'react';
import {
  Activity,
  ArrowLeft,
  ArrowUpRight,
  BarChart3,
  Building2,
  LayoutDashboard,
  LogOut,
  Menu,
  Search,
  Store,
  UsersRound,
  LibraryBig,
  MapPinned,
  ImagePlus,
  Plus,
  Edit3,
  EllipsisVertical,
  SlidersHorizontal,
  Trash2,
  X,
  MessageSquareText,
  ChevronLeft,
  TrendingUp,
  Sparkles,
  SearchCheck,
  Star,
  Save,
} from 'lucide-react';
import { createStore, createUser, deleteStore, enrichTenantDescription, getAllStores, getKnowledgeSources, getUserConversation, getUserConversations, getUsers, updateStore, updateStorefrontImage, updateUserAccess, validateTenantSearch } from '../api';
import type { KnowledgeSourceDto, ManagedUserDto, ShopperConversationHistoryDto, ShopperConversationSummaryDto, StoreDto, TenantDescriptionEnrichmentResult, TenantSearchValidationResult, UserProfileDto, UserRole } from '../types';
import { KnowledgeWorkspace, MarkdownEditor } from './KnowledgeWorkspace';
import { BusinessIntelligenceView } from './BusinessIntelligenceView';
import { mallConfig } from '../mallConfig';
import { MarkdownDescription } from './MarkdownDescription';
const NodeBuilder = lazy(() => import('./NodeBuilder').then((module) => ({ default: module.NodeBuilder })));

function fileToDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => typeof reader.result === 'string' ? resolve(reader.result) : reject(new Error('Unable to read the image.'));
    reader.onerror = () => reject(new Error('Unable to read the image.'));
    reader.readAsDataURL(file);
  });
}

export type AdminSection = 'overview' | 'bi' | 'knowledge' | 'map' | 'tenants' | 'users';
const adminSectionPaths: Record<AdminSection, string> = {
  overview: '/admin/overview', bi: '/admin/business-intelligence', knowledge: '/admin/knowledge', map: '/admin/mall-map', tenants: '/admin/tenants', users: '/admin/users',
};

const storeCategories = [
  [1, 'Fashion'], [2, 'Electronics'], [3, 'Food & beverage'], [4, 'Entertainment'],
  [5, 'Beauty & wellness'], [6, 'Supermarket'], [7, 'Department store'], [8, 'Services'], [99, 'Other'],
] as const;

const storeCategoryLabel = (category: number) => storeCategories.find(([value]) => value === category)?.[1] ?? 'Other';

interface AdminAreaProps {
  profile: UserProfileDto;
  section: AdminSection;
  onSectionChange: (section: AdminSection) => void;
  onExit: () => void;
  onSignOut: () => void;
}

export function AdminArea({ profile, section, onSectionChange, onExit, onSignOut }: AdminAreaProps) {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [tenants, setTenants] = useState<StoreDto[]>([]);
  const [knowledgeSources, setKnowledgeSources] = useState<KnowledgeSourceDto[]>([]);
  const [users, setUsers] = useState<ManagedUserDto[]>([]);
  const [tenantSearch, setTenantSearch] = useState('');
  const [tenantCategoryFilter, setTenantCategoryFilter] = useState('all');
  const [tenantStatusFilter, setTenantStatusFilter] = useState('all');
  const [tenantFloorFilter, setTenantFloorFilter] = useState('all');
  const [userSearch, setUserSearch] = useState('');
  const [userRoleFilter, setUserRoleFilter] = useState('all');
  const [userTenantFilter, setUserTenantFilter] = useState('all');
  const [userError, setUserError] = useState('');
  const [tenantImageBusy, setTenantImageBusy] = useState<string | null>(null);
  const [editingTenant, setEditingTenant] = useState<StoreDto | null>(null);
  const [creatingTenant, setCreatingTenant] = useState(false);
  const [creatingUser, setCreatingUser] = useState(false);
  const [editingUser, setEditingUser] = useState<ManagedUserDto | null>(null);
  const [historyUser, setHistoryUser] = useState<ManagedUserDto | null>(null);
  const [userConversations, setUserConversations] = useState<ShopperConversationSummaryDto[]>([]);
  const [selectedConversation, setSelectedConversation] = useState<ShopperConversationHistoryDto | null>(null);
  const [conversationBusy, setConversationBusy] = useState(false);
  const [conversationError, setConversationError] = useState('');
  const [managementBusy, setManagementBusy] = useState(false);

  useEffect(() => {
    Promise.all([
      getAllStores(),
      getKnowledgeSources(),
      profile.role === 'Admin' ? getUsers() : Promise.resolve([]),
    ])
      .then(([tenantData, sourceData, userData]) => {
        setTenants(tenantData);
        setKnowledgeSources(sourceData);
        setUsers(userData);
      })
      .catch(() => undefined);
  }, [profile]);

  const isAdmin = profile.role === 'Admin';
  const currentTenant = profile.tenantId ? tenants.find((tenant) => tenant.id === profile.tenantId) : undefined;
  const tenantFloors = useMemo(() => [...new Set(tenants.map((tenant) => tenant.floorNumber))].sort((a, b) => a - b), [tenants]);
  const filteredTenants = useMemo(() => {
    const query = tenantSearch.trim().toLowerCase();
    return tenants.filter((tenant) => (tenantCategoryFilter === 'all' || tenant.category === Number(tenantCategoryFilter)) &&
      (tenantStatusFilter === 'all' || (tenantStatusFilter === 'active' ? tenant.isActive : !tenant.isActive)) &&
      (tenantFloorFilter === 'all' || tenant.floorNumber === Number(tenantFloorFilter)) &&
      (!query || [
      tenant.name, tenant.description, storeCategoryLabel(tenant.category), tenant.contactEmail,
      tenant.contactPhone, tenant.openingHours, tenant.unitNumber, `level ${tenant.floorNumber}`,
    ].some((value) => value?.toLowerCase().includes(query))));
  }, [tenantCategoryFilter, tenantFloorFilter, tenantSearch, tenantStatusFilter, tenants]);
  const filteredUsers = useMemo(() => {
    const query = userSearch.trim().toLowerCase();
    return users.filter((user) => (userRoleFilter === 'all' || user.role === userRoleFilter) &&
      (userTenantFilter === 'all' || (userTenantFilter === 'unassigned' ? !user.tenantId : user.tenantId === userTenantFilter)) &&
      (!query || user.fullName.toLowerCase().includes(query) ||
      user.userName.toLowerCase().includes(query) ||
      user.email?.toLowerCase().includes(query) ||
      user.role.toLowerCase().includes(query) ||
      user.tenantName?.toLowerCase().includes(query))
    );
  }, [userRoleFilter, userSearch, userTenantFilter, users]);

  const navigation: Array<{ id: AdminSection; label: string; icon: typeof LayoutDashboard; adminOnly?: boolean }> = [
    { id: 'overview', label: 'Overview', icon: LayoutDashboard },
    { id: 'bi', label: 'Business intelligence', icon: TrendingUp },
    { id: 'knowledge', label: 'Knowledge', icon: LibraryBig },
    { id: 'map', label: 'Mall map', icon: MapPinned, adminOnly: true },
    { id: 'tenants', label: 'Tenants', icon: Store, adminOnly: true },
    { id: 'users', label: 'Users', icon: UsersRound, adminOnly: true },
  ];

  const switchSection = (next: AdminSection) => {
    onSectionChange(next);
    setMobileMenuOpen(false);
  };

  const changeUserAccess = async (user: ManagedUserDto, role: Exclude<UserRole, 'Guest'>, tenantId?: string | null) => {
    setUserError('');
    const nextTenantId = role === 'TenantStaff' ? (tenantId || user.tenantId || tenants[0]?.id) : null;
    if (role === 'TenantStaff' && !nextTenantId) {
      setUserError('Create a tenant before assigning tenant staff.');
      return;
    }

    try {
      setManagementBusy(true);
      const updated = await updateUserAccess(user.id, role, nextTenantId);
      setUsers((current) => current.map((item) => item.id === updated.id ? updated : item));
      setEditingUser(null);
    } catch (error) {
      setUserError(error instanceof Error ? error.message : 'Unable to update user access.');
    } finally {
      setManagementBusy(false);
    }
  };

  const addUser = async (input: {
    fullName: string;
    email: string;
    password: string;
    role: Exclude<UserRole, 'Guest'>;
    tenantId: string | null;
  }) => {
    setUserError('');
    try {
      setManagementBusy(true);
      const created = await createUser(input);
      setUsers((current) => [...current, created].sort((a, b) => a.fullName.localeCompare(b.fullName)));
      setCreatingUser(false);
    } catch (error) {
      setUserError(error instanceof Error ? error.message : 'Unable to create user.');
    } finally {
      setManagementBusy(false);
    }
  };

  const openUserHistory = async (user: ManagedUserDto) => {
    setHistoryUser(user);
    setUserConversations([]);
    setSelectedConversation(null);
    setConversationError('');
    setConversationBusy(true);
    try {
      setUserConversations(await getUserConversations(user.id));
    } catch (error) {
      setConversationError(error instanceof Error ? error.message : 'Unable to load conversation history.');
    } finally {
      setConversationBusy(false);
    }
  };

  const openUserConversation = async (sessionId: string) => {
    if (!historyUser) return;
    setConversationError('');
    setConversationBusy(true);
    try {
      setSelectedConversation(await getUserConversation(historyUser.id, sessionId));
    } catch (error) {
      setConversationError(error instanceof Error ? error.message : 'Unable to load the conversation.');
    } finally {
      setConversationBusy(false);
    }
  };

  const saveTenant = async (input: Omit<StoreDto, 'id' | 'navigationNodeId' | 'storefrontImageUrl'>) => {
    if (!editingTenant) return;
    setUserError(''); setManagementBusy(true);
    try {
      const updated = await updateStore(editingTenant.id, input);
      setTenants((current) => current.map((item) => item.id === updated.id ? updated : item));
      setEditingTenant(null);
    } catch (error) {
      setUserError(error instanceof Error ? error.message : 'Unable to update tenant.');
    } finally {
      setManagementBusy(false);
    }
  };

  const saveTenantAndValidate = async (input: Omit<StoreDto, 'id' | 'navigationNodeId' | 'storefrontImageUrl'>) => {
    if (!editingTenant) throw new Error('Tenant is no longer selected.');
    setUserError(''); setManagementBusy(true);
    try {
      const saved = await updateStore(editingTenant.id, input);
      setTenants((current) => current.map((item) => item.id === saved.id ? saved : item));
      setEditingTenant(saved);
      const result = await validateTenantSearch(saved.id, saved.description);
      const updated = { ...saved, searchValidationRating: result.rating, searchValidationSummary: result.summary,
        searchValidationDescriptionHash: result.descriptionHash, searchValidationAtUtc: result.validatedAtUtc, searchValidationIsCurrent: true };
      setTenants((current) => current.map((item) => item.id === updated.id ? updated : item));
      setEditingTenant(updated);
      return result;
    } catch (error) {
      setUserError(error instanceof Error ? error.message : 'Unable to save and validate tenant.');
      throw error;
    } finally { setManagementBusy(false); }
  };

  const addTenant = async (input: Omit<StoreDto, 'id' | 'navigationNodeId' | 'storefrontImageUrl'>, imageFile?: File) => {
    setUserError(''); setManagementBusy(true);
    try {
      const { isActive: _isActive, ...createInput } = input;
      const created = await createStore(createInput);
      setTenants((current) => [...current, created].sort((a, b) => a.name.localeCompare(b.name)));
      if (imageFile) await updateTenantImage(created, imageFile);
      setCreatingTenant(false);
    } catch (error) {
      setUserError(error instanceof Error ? error.message : 'Unable to create tenant.');
    } finally {
      setManagementBusy(false);
    }
  };

  const removeTenant = async (tenant: StoreDto) => {
    if (!window.confirm(`Delete “${tenant.name}”? This cannot be undone.`)) return;
    setUserError(''); setManagementBusy(true);
    try {
      await deleteStore(tenant.id);
      setTenants((current) => current.filter((item) => item.id !== tenant.id));
    } catch (error) {
      setUserError(error instanceof Error ? error.message : 'Unable to delete tenant.');
    } finally {
      setManagementBusy(false);
    }
  };

  const updateTenantImage = async (tenant: StoreDto, file?: File) => {
    if (!file) return;
    if (!file.type.startsWith('image/') || file.size > 10 * 1024 * 1024) {
      setUserError('Choose a PNG, JPEG, WebP or GIF image up to 10 MB.');
      return;
    }
    setTenantImageBusy(tenant.id); setUserError('');
    try {
      const dataUrl = await fileToDataUrl(file);
      const updated = await updateStorefrontImage(tenant.id, dataUrl);
      setTenants((current) => current.map((item) => item.id === updated.id ? updated : item));
      if (editingTenant?.id === updated.id) setEditingTenant(updated);
    } catch (error) {
      setUserError(error instanceof Error ? error.message : 'Unable to save the tenant image.');
    } finally { setTenantImageBusy(null); }
  };

  const tenantImage = (tenant: StoreDto) => tenant.storefrontImageUrl || undefined;

  return (
    <div data-theme="light" className="mall-shell min-h-screen text-base-content lg:grid lg:grid-cols-[268px_1fr]">
      <aside className={`${mobileMenuOpen ? 'flex' : 'hidden'} fixed inset-0 z-50 flex-col border-r border-base-300 bg-base-100 p-5 text-base-content lg:sticky lg:top-0 lg:flex lg:h-screen`}>
        <div className="flex items-center justify-between px-2 py-2">
          <div className="flex items-center gap-3"><span className="grid size-10 place-items-center rounded-xl bg-primary text-primary-content"><Building2 size={18} /></span><strong className="block text-lg tracking-tight">{mallConfig.platformName}</strong></div>
          <button className="btn btn-ghost btn-sm lg:hidden" aria-label="Close menu" onClick={() => setMobileMenuOpen(false)}>Close</button>
        </div>
        <div className="my-4 border-t border-base-300" />
        <nav className="flex flex-col gap-1" aria-label="Admin navigation">
          {navigation.filter((item) => !item.adminOnly || isAdmin).map(({ id, label, icon: Icon }) => (
            <a
              key={id}
              href={adminSectionPaths[id]}
              className={`flex w-full items-center gap-3 rounded-xl border px-3 py-3 text-left text-sm font-semibold transition-colors ${section === id ? 'border-primary/15 bg-primary/10 text-base-content' : 'border-transparent text-base-content/55 hover:bg-base-200 hover:text-base-content'}`}
              onClick={(event) => { event.preventDefault(); switchSection(id); }}
            >
              <Icon className="shrink-0" size={16} />
              <span>{label}</span>
            </a>
          ))}
        </nav>
        <div className="mt-auto space-y-2 border-t border-base-300 pt-4">
          <div className="px-3 pb-2"><div className="text-sm font-semibold">{profile.fullName}</div><div className="mt-1 text-xs text-base-content/45">{profile.role === 'Admin' ? 'Administrator' : profile.tenantName}</div></div>
          <button className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-base-content/60 hover:bg-base-200 hover:text-base-content" onClick={onExit}><ArrowLeft size={15} /> Public store</button>
          <button className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-base-content/60 hover:bg-base-200 hover:text-base-content" onClick={onSignOut}><LogOut size={15} /> Sign out</button>
        </div>
      </aside>

      <div className="min-w-0">
        <header className="sticky top-0 z-30 flex h-[4.5rem] items-center justify-between border-b border-base-300 bg-base-100/95 px-4 backdrop-blur md:px-8">
          <div className="flex items-center gap-3"><button className="btn btn-ghost btn-sm lg:hidden" aria-label="Open menu" onClick={() => setMobileMenuOpen(true)}><Menu size={18} /></button><h1 className="font-semibold">{navigation.find((item) => item.id === section)?.label ?? 'Overview'}</h1></div>
          <button className="text-sm text-base-content/60 hover:text-base-content" onClick={onSignOut}>Log out</button>
        </header>

        <main className="p-4 md:p-8 lg:p-10">
          {section === 'overview' && (
            <div className="mx-auto max-w-7xl space-y-6">
              <section className="admin-page-header flex flex-wrap items-center justify-between gap-5 p-6 md:p-8"><div><h2 className="text-3xl font-bold tracking-[-0.04em]">{isAdmin ? 'Mall overview' : `${profile.tenantName} overview`}</h2><p className="mt-2 text-sm leading-6 text-base-content/55">Monitor your operation and jump directly to the areas that need attention.</p></div><span className="inline-flex items-center gap-2 rounded-full bg-success/10 px-4 py-2 text-xs font-bold text-success"><span className="size-2 rounded-full bg-success" /> Live data</span></section>
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
                {isAdmin && <OverviewMetric label="Active tenants" value={tenants.filter((tenant) => tenant.isActive).length} detail={`${tenants.length} total tenants`} icon={Store} tone="primary" />}
                {isAdmin && <OverviewMetric label="Shoppers" value={users.filter((user) => user.role === 'Shopper').length} detail="Registered accounts" icon={UsersRound} tone="secondary" />}
                {isAdmin && <OverviewMetric label="Guest profiles" value={users.filter((user) => user.role === 'Guest').length} detail="Anonymous sessions saved" icon={Activity} tone="accent" />}
                <OverviewMetric label="Knowledge sources" value={knowledgeSources.length} detail="Available to your scope" icon={LibraryBig} tone="warning" />
                <OverviewMetric label="Published sources" value={knowledgeSources.filter((source) => source.status === 'Published').length} detail={`${knowledgeSources.filter((source) => source.status !== 'Published').length} not published`} icon={BarChart3} tone="success" />
              </div>
              {!isAdmin && currentTenant && <section className="surface-card flex flex-wrap items-center gap-4 p-5"><label className="block size-20 shrink-0 cursor-pointer overflow-hidden rounded-xl border border-base-300 bg-base-200" title="Upload storefront image">{tenantImage(currentTenant) ? <img src={tenantImage(currentTenant)} alt="" className="h-full w-full object-cover" /> : <span className="grid h-full place-items-center text-base-content/35"><ImagePlus size={22} /></span>}<input hidden type="file" accept="image/png,image/jpeg,image/webp,image/gif" disabled={tenantImageBusy === currentTenant.id} onChange={(event) => { void updateTenantImage(currentTenant, event.target.files?.[0]); event.target.value = ''; }} /></label><div><h3 className="font-bold">Storefront image</h3><p className="mt-1 text-sm text-base-content/50">Shown on your public Directory card.</p><label className="btn btn-ghost btn-sm mt-3 cursor-pointer border border-base-300"><ImagePlus size={14} />{tenantImage(currentTenant) ? 'Change image' : 'Add image'}<input hidden type="file" accept="image/png,image/jpeg,image/webp,image/gif" disabled={tenantImageBusy === currentTenant.id} onChange={(event) => { void updateTenantImage(currentTenant, event.target.files?.[0]); event.target.value = ''; }} /></label></div></section>}
              <div className="grid gap-5 lg:grid-cols-[1.1fr_.9fr]">
                <section className="surface-card p-5 md:p-6"><div className="flex items-start justify-between gap-4"><div><h3 className="font-bold">Knowledge readiness</h3><p className="mt-1 text-xs text-base-content/45">Publishing health for sources in your scope.</p></div><span className="badge badge-ghost">{knowledgeSources.length === 0 ? 0 : Math.round(knowledgeSources.filter((source) => source.status === 'Published').length / knowledgeSources.length * 100)}% published</span></div><div className="mt-6 h-2 overflow-hidden rounded-full bg-base-300"><div className="h-full rounded-full bg-success transition-all" style={{ width: `${knowledgeSources.length === 0 ? 0 : knowledgeSources.filter((source) => source.status === 'Published').length / knowledgeSources.length * 100}%` }} /></div><div className="mt-5 flex flex-wrap gap-x-8 gap-y-3 text-sm"><span><strong>{knowledgeSources.filter((source) => source.status === 'Published').length}</strong> <span className="text-base-content/50">published</span></span><span><strong>{knowledgeSources.filter((source) => source.status !== 'Published').length}</strong> <span className="text-base-content/50">need review</span></span></div></section>
                <section className="surface-card p-5 md:p-6"><h3 className="font-bold">Quick actions</h3><p className="mt-1 text-xs text-base-content/45">Continue with a core operation.</p><div className="mt-5 space-y-2"><button className="flex w-full items-center gap-3 rounded-xl border border-base-300 p-4 text-left transition hover:border-primary/30 hover:bg-primary/5" onClick={() => switchSection('bi')}><span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary"><TrendingUp size={17} /></span><span className="min-w-0 flex-1"><strong className="block text-sm">Review business intelligence</strong><span className="mt-0.5 block text-xs text-base-content/45">Inspect shopper demand and engagement.</span></span><ArrowUpRight size={16} className="text-base-content/35" /></button><button className="flex w-full items-center gap-3 rounded-xl border border-base-300 p-4 text-left transition hover:border-primary/30 hover:bg-primary/5" onClick={() => switchSection('knowledge')}><span className="grid size-9 shrink-0 place-items-center rounded-lg bg-warning/10 text-warning"><LibraryBig size={17} /></span><span className="min-w-0 flex-1"><strong className="block text-sm">Manage knowledge</strong><span className="mt-0.5 block text-xs text-base-content/45">Review, update, and publish sources.</span></span><ArrowUpRight size={16} className="text-base-content/35" /></button></div></section>
              </div>
            </div>
          )}

          {section === 'knowledge' && <div className="mx-auto max-w-7xl"><KnowledgeWorkspace isAdmin={isAdmin} /></div>}

          {section === 'bi' && <BusinessIntelligenceView profile={profile} />}

          {section === 'map' && isAdmin && <Suspense fallback={<div className="grid min-h-96 place-items-center text-sm text-base-content/45">Loading visual builder…</div>}><NodeBuilder /></Suspense>}

          {section === 'tenants' && isAdmin && (
            <div className="mx-auto max-w-7xl space-y-6">
              <section className="admin-page-header flex flex-wrap items-end justify-between gap-5 p-6 md:p-8"><div><h2 className="text-3xl font-bold tracking-[-0.04em]">Tenants</h2><p className="mt-2 text-sm text-base-content/55">Manage tenant profiles, locations, contacts and storefront images.</p></div><button className="btn btn-primary" onClick={() => setCreatingTenant(true)}><Plus size={16} /> Create tenant</button></section>
              {userError && <div className="alert alert-error py-3 text-sm">{userError}</div>}
              <section className="surface-card p-5"><div className="flex flex-col gap-3 md:flex-row"><label className="input input-bordered flex flex-1 items-center gap-2"><Search size={16} className="text-base-content/40" /><input className="grow" value={tenantSearch} onChange={(event) => setTenantSearch(event.target.value)} placeholder="Search tenants" /></label><TenantFilterMenu category={tenantCategoryFilter} setCategory={setTenantCategoryFilter} status={tenantStatusFilter} setStatus={setTenantStatusFilter} floor={tenantFloorFilter} setFloor={setTenantFloorFilter} floors={tenantFloors} /></div></section>
              <section className="overflow-hidden rounded-2xl border border-base-300 bg-base-100">
                <div className="flex items-center justify-between border-b border-base-300 px-5 py-4"><h3 className="font-bold">Tenants</h3><span className="text-xs text-base-content/45">{filteredTenants.length} items</span></div>
                <div className="overflow-x-auto"><table className="table"><thead><tr><th>Image</th><th>Tenant</th><th>Category</th><th>Location</th><th>Search rank</th><th>Status</th><th className="text-right">Actions</th></tr></thead><tbody>{filteredTenants.map((tenant) => { const image = tenantImage(tenant); return <tr key={tenant.id} className="hover:bg-base-200/50"><td><div className="block size-14 overflow-hidden rounded-lg border border-base-300 bg-gradient-to-br from-base-200 to-base-300">{image ? <img src={image} alt="" className="h-full w-full object-cover" /> : <span className="grid h-full place-items-center text-base-content/30"><Store size={20} /><span className="sr-only">Default storefront image</span></span>}</div></td><td><button className="text-left font-semibold hover:text-primary" onClick={() => setEditingTenant(tenant)}>{tenant.name}</button><div className="text-xs text-base-content/45">{tenant.contactEmail || 'No contact email'}</div></td><td>{storeCategoryLabel(tenant.category)}</td><td>Level {tenant.floorNumber} · {tenant.unitNumber}</td><td><div className="flex items-center gap-1" title={tenant.searchValidationSummary || 'Search validation has not been run'}>{tenant.searchValidationRating ? <><span className={`flex ${tenant.searchValidationIsCurrent ? 'text-warning' : 'text-base-content/30'}`}>{[1,2,3,4,5].map((star) => <Star key={star} size={13} fill={star <= tenant.searchValidationRating! ? 'currentColor' : 'none'} />)}</span><span className="text-xs">{tenant.searchValidationRating}/5</span>{!tenant.searchValidationIsCurrent && <span className="badge badge-warning badge-xs">Stale</span>}</> : <span className="badge badge-ghost badge-sm">Not run</span>}</div>{tenant.searchValidationAtUtc && <div className="mt-1 text-[10px] text-base-content/40">{new Date(tenant.searchValidationAtUtc).toLocaleDateString()}</div>}</td><td><span className={`badge badge-sm ${tenant.isActive ? 'badge-success badge-soft' : 'badge-ghost'}`}>{tenant.isActive ? 'Active' : 'Inactive'}</span></td><td><div className="flex justify-end gap-1"><button className="btn btn-ghost btn-xs" onClick={() => setEditingTenant(tenant)}><Edit3 size={13} /> Edit</button><div className="dropdown dropdown-end"><button type="button" tabIndex={0} className="btn btn-ghost btn-xs btn-square" aria-label={`More actions for ${tenant.name}`}><EllipsisVertical size={15} /></button><ul tabIndex={0} className="menu dropdown-content z-20 mt-1 w-40 rounded-box border border-base-300 bg-base-100 p-1 shadow-lg"><li><button className="text-error" disabled={managementBusy} onClick={() => void removeTenant(tenant)}><Trash2 size={13} /> Delete</button></li></ul></div></div></td></tr>; })}{filteredTenants.length === 0 && <tr><td colSpan={7} className="py-10 text-center text-base-content/45">No tenants match this search.</td></tr>}</tbody></table></div>
              </section>
            </div>
          )}

          {section === 'users' && isAdmin && (
            <div className="mx-auto max-w-7xl space-y-5">
              <div className="admin-page-header flex flex-wrap items-end justify-between gap-5 p-6 md:p-8"><div><h2 className="text-3xl font-bold tracking-[-0.04em]">Users</h2><p className="mt-2 text-sm text-base-content/55">Create accounts, view guest activity and manage access.</p></div><button className="btn btn-primary" onClick={() => { setUserError(''); setCreatingUser(true); }}><Plus size={16} /> Create user</button></div>
              {userError && <div className="alert alert-error py-3 text-sm">{userError}</div>}
              <section className="surface-card p-5"><div className="flex flex-col gap-3 md:flex-row"><label className="input input-bordered flex flex-1 items-center gap-2"><Search size={16} className="text-base-content/40" /><input className="grow" value={userSearch} onChange={(event) => setUserSearch(event.target.value)} placeholder="Search users or tenants" /></label><UserFilterMenu role={userRoleFilter} setRole={setUserRoleFilter} tenantId={userTenantFilter} setTenantId={setUserTenantFilter} tenants={tenants} /></div></section>
              <div className="overflow-hidden rounded-2xl border border-base-300 bg-base-100">
                <div className="flex items-center justify-between border-b border-base-300 px-5 py-4"><h3 className="font-bold">Users</h3><span className="text-xs text-base-content/45">{filteredUsers.length} items</span></div>
                <div className="overflow-x-auto">
                <table className="table">
                  <thead><tr><th>User</th><th>Type</th><th>Created</th><th>Last active</th><th className="text-right">Actions</th></tr></thead>
                  <tbody>
                    {filteredUsers.map((user) => {
                      const isGuest = user.role === 'Guest';
                      const isCurrentAdmin = user.id === profile.id;
                      return (
                        <tr key={user.id} className="hover:bg-base-200/50">
                          <td>{!isGuest && !isCurrentAdmin ? <button className="text-left font-semibold hover:text-primary" onClick={() => setEditingUser(user)}>{user.fullName}</button> : <div className="font-semibold">{user.fullName}</div>}<div className="mt-1 flex flex-wrap items-center gap-2"><span className="text-xs text-base-content/45">{user.email || user.userName}</span>{user.tenantName && <span className="badge badge-primary badge-soft badge-sm">{user.tenantName}</span>}</div></td>
                          <td><span className="badge badge-ghost badge-sm">{user.role === 'TenantStaff' ? 'Tenant staff' : user.role}</span></td>
                          <td className="whitespace-nowrap text-xs text-base-content/60">{new Date(user.createdAtUtc).toLocaleDateString()}</td>
                          <td className="whitespace-nowrap text-xs text-base-content/60">{new Date(user.lastActiveAtUtc).toLocaleString()}</td>
                          <td><div className="flex justify-end gap-1">{!isGuest && !isCurrentAdmin && <button className="btn btn-ghost btn-xs" onClick={() => setEditingUser(user)}><Edit3 size={13} /> Edit</button>}<div className="dropdown dropdown-end"><button type="button" tabIndex={0} className="btn btn-ghost btn-xs btn-square" aria-label={`More actions for ${user.fullName}`}><EllipsisVertical size={15} /></button><ul tabIndex={0} className="menu dropdown-content z-20 mt-1 w-48 rounded-box border border-base-300 bg-base-100 p-1 shadow-lg"><li><button onClick={() => void openUserHistory(user)}><MessageSquareText size={13} /> Conversation history</button></li>{!isGuest && !isCurrentAdmin && <li><button onClick={() => setEditingUser(user)}><Edit3 size={13} /> Edit access</button></li>}</ul></div></div></td>
                        </tr>
                      );
                    })}
                    {filteredUsers.length === 0 && <tr><td colSpan={5} className="py-10 text-center text-base-content/45">No users match this search.</td></tr>}
                  </tbody>
                </table>
                </div>
              </div>
            </div>
          )}

          {editingTenant && <TenantManagementModal tenant={editingTenant} image={tenantImage(editingTenant)} busy={managementBusy} imageBusy={tenantImageBusy === editingTenant.id} onImageChange={(file) => updateTenantImage(editingTenant, file)} onClose={() => setEditingTenant(null)} onSave={saveTenant} onSaveAndValidate={saveTenantAndValidate} />}
          {creatingTenant && <TenantManagementModal busy={managementBusy} imageBusy={false} onClose={() => setCreatingTenant(false)} onSave={addTenant} />}
          {creatingUser && <CreateUserModal tenants={tenants} busy={managementBusy} onClose={() => setCreatingUser(false)} onSave={addUser} />}
          {editingUser && <UserManagementModal user={editingUser} tenants={tenants} busy={managementBusy} onClose={() => setEditingUser(null)} onSave={(role, tenantId) => changeUserAccess(editingUser, role, tenantId)} />}
          {historyUser && <ConversationHistoryModal user={historyUser} conversations={userConversations} conversation={selectedConversation} busy={conversationBusy} error={conversationError} onOpen={openUserConversation} onBack={() => { setSelectedConversation(null); setConversationError(''); }} onClose={() => { setHistoryUser(null); setSelectedConversation(null); setConversationError(''); }} />}

        </main>
      </div>
    </div>
  );
}

function OverviewMetric({ label, value, detail, icon: Icon, tone }: {
  label: string;
  value: number;
  detail: string;
  icon: typeof Store;
  tone: 'primary' | 'secondary' | 'accent' | 'warning' | 'success';
}) {
  const tones = {
    primary: 'bg-primary/10 text-primary',
    secondary: 'bg-secondary/10 text-secondary',
    accent: 'bg-accent/10 text-accent',
    warning: 'bg-warning/10 text-warning',
    success: 'bg-success/10 text-success',
  };
  return <div className="surface-card p-5"><div className="flex items-start justify-between gap-3"><div className="text-xs font-bold uppercase tracking-wider text-base-content/40">{label}</div><span className={`grid size-9 shrink-0 place-items-center rounded-xl ${tones[tone]}`}><Icon size={17} /></span></div><div className="mt-4 text-3xl font-bold">{value}</div><div className="mt-2 text-xs text-base-content/45">{detail}</div></div>;
}

function ConversationHistoryModal({ user, conversations, conversation, busy, error, onOpen, onBack, onClose }: {
  user: ManagedUserDto;
  conversations: ShopperConversationSummaryDto[];
  conversation: ShopperConversationHistoryDto | null;
  busy: boolean;
  error: string;
  onOpen: (sessionId: string) => Promise<void>;
  onBack: () => void;
  onClose: () => void;
}) {
  return <div className="fixed inset-0 z-[70] grid place-items-center bg-neutral/45 p-3 backdrop-blur-sm" role="dialog" aria-modal="true" aria-labelledby="conversation-history-title" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <section className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-base-300 bg-base-100 shadow-2xl">
      <header className="flex items-start justify-between gap-4 border-b border-base-300 px-5 py-4">
        <div className="flex min-w-0 items-start gap-2">{conversation && <button className="btn btn-ghost btn-sm btn-circle -ml-2" onClick={onBack} aria-label="Back to conversations"><ChevronLeft size={18} /></button>}<div className="min-w-0"><h2 id="conversation-history-title" className="truncate text-lg font-bold">{conversation ? 'Conversation details' : `${user.fullName} conversations`}</h2><p className="mt-1 text-xs text-base-content/50">{user.email || user.userName} · Read-only admin view</p></div></div>
        <button className="btn btn-ghost btn-sm btn-circle" onClick={onClose} aria-label="Close conversation history"><X size={18} /></button>
      </header>
      <div className="min-h-64 flex-1 overflow-y-auto p-4 md:p-6">
        {busy && <div className="grid min-h-56 place-items-center"><span className="loading loading-spinner loading-md text-primary" /></div>}
        {!busy && error && <div className="alert alert-error text-sm">{error}</div>}
        {!busy && !error && !conversation && conversations.length === 0 && <div className="grid min-h-56 place-items-center text-center"><div><MessageSquareText className="mx-auto text-base-content/25" size={34} /><p className="mt-3 font-semibold">No saved conversations</p><p className="mt-1 text-sm text-base-content/50">This user has not started a saved chat.</p></div></div>}
        {!busy && !error && !conversation && conversations.length > 0 && <div className="space-y-2">{conversations.map((item) => <button key={item.sessionId} className="block w-full rounded-xl border border-base-300 p-4 text-left transition hover:border-primary/35 hover:bg-primary/5" onClick={() => void onOpen(item.sessionId)}><div className="flex items-start justify-between gap-4"><div className="min-w-0"><div className="truncate font-bold">{item.title}</div><p className="mt-1 line-clamp-2 text-sm text-base-content/55">{item.lastMessage}</p></div><span className="badge badge-ghost badge-sm shrink-0">{item.surfaceContext}</span></div><div className="mt-3 text-xs text-base-content/40">{new Date(item.updatedAtUtc).toLocaleString()} · {item.turnCount} messages</div></button>)}</div>}
        {!busy && !error && conversation && <div className="space-y-4">{conversation.turns.map((turn) => <article key={turn.id} className={`flex ${turn.role === 'user' ? 'justify-end' : 'justify-start'}`}><div className={`max-w-[85%] rounded-2xl px-4 py-3 ${turn.role === 'user' ? 'bg-primary text-primary-content' : 'border border-base-300 bg-base-200/70 text-base-content'}`}><div className="mb-1 text-[10px] font-bold uppercase tracking-wider opacity-60">{turn.role === 'user' ? user.fullName : `${mallConfig.shortName} assistant`}</div><p className="whitespace-pre-wrap text-sm leading-6">{turn.message}</p><div className="mt-2 text-[10px] opacity-55">{new Date(turn.createdAtUtc).toLocaleString()}</div></div></article>)}</div>}
      </div>
    </section>
  </div>;
}

function TenantFilterMenu({ category, setCategory, status, setStatus, floor, setFloor, floors }: {
  category: string; setCategory: (value: string) => void;
  status: string; setStatus: (value: string) => void;
  floor: string; setFloor: (value: string) => void;
  floors: number[];
}) {
  const activeCount = [category, status, floor].filter((value) => value !== 'all').length;
  return <details className="dropdown dropdown-end md:self-start"><summary className={`btn list-none border-base-300 ${activeCount ? 'btn-primary' : 'btn-ghost bg-base-100'}`}><SlidersHorizontal size={16} /> Filters{activeCount > 0 && <span className="badge badge-sm">{activeCount}</span>}</summary><div className="dropdown-content z-40 mt-2 w-[min(92vw,40rem)] rounded-2xl border border-base-300 bg-base-100 p-5 shadow-2xl"><div className="flex items-center justify-between border-b border-base-300 pb-3"><div><div className="font-bold">Filter tenants</div><div className="mt-1 text-xs text-base-content/45">Filter by category, operating status, or floor.</div></div><button className="btn btn-ghost btn-xs" disabled={!activeCount} onClick={() => { setCategory('all'); setStatus('all'); setFloor('all'); }}>Clear all</button></div><div className="mt-4 grid gap-5 md:grid-cols-3"><label className="grid content-start gap-2"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Category</span><select className="select select-bordered w-full" value={category} onChange={(event) => setCategory(event.target.value)}><option value="all">All categories</option>{storeCategories.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><div className="grid content-start gap-2"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Status</span>{[['all', 'All statuses'], ['active', 'Active'], ['inactive', 'Inactive']].map(([value, label]) => <label key={value} className="flex cursor-pointer items-center gap-2 text-sm"><input className="radio radio-primary radio-sm" type="radio" name="tenant-status-filter" checked={status === value} onChange={() => setStatus(value)} />{label}</label>)}</div><label className="grid content-start gap-2"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Floor</span><select className="select select-bordered w-full" value={floor} onChange={(event) => setFloor(event.target.value)}><option value="all">All floors</option>{floors.map((value) => <option key={value} value={value}>Level {value}</option>)}</select></label></div></div></details>;
}

function UserFilterMenu({ role, setRole, tenantId, setTenantId, tenants }: {
  role: string; setRole: (value: string) => void;
  tenantId: string; setTenantId: (value: string) => void;
  tenants: StoreDto[];
}) {
  const activeCount = [role, tenantId].filter((value) => value !== 'all').length;
  return <details className="dropdown dropdown-end md:self-start"><summary className={`btn list-none border-base-300 ${activeCount ? 'btn-primary' : 'btn-ghost bg-base-100'}`}><SlidersHorizontal size={16} /> Filters{activeCount > 0 && <span className="badge badge-sm">{activeCount}</span>}</summary><div className="dropdown-content z-40 mt-2 w-[min(92vw,36rem)] rounded-2xl border border-base-300 bg-base-100 p-5 shadow-2xl"><div className="flex items-center justify-between border-b border-base-300 pb-3"><div><div className="font-bold">Filter users</div><div className="mt-1 text-xs text-base-content/45">Filter by account role or tenant assignment.</div></div><button className="btn btn-ghost btn-xs" disabled={!activeCount} onClick={() => { setRole('all'); setTenantId('all'); }}>Clear all</button></div><div className="mt-4 grid gap-5 md:grid-cols-2"><div className="grid content-start gap-2"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Role</span>{[['all', 'All roles'], ['Guest', 'Guest'], ['Shopper', 'Shopper'], ['TenantStaff', 'Tenant staff'], ['Admin', 'Administrator']].map(([value, label]) => <label key={value} className="flex cursor-pointer items-center gap-2 text-sm"><input className="radio radio-primary radio-sm" type="radio" name="user-role-filter" checked={role === value} onChange={() => setRole(value)} />{label}</label>)}</div><label className="grid content-start gap-2"><span className="text-xs font-bold uppercase tracking-wider text-base-content/45">Tenant assignment</span><select className="select select-bordered w-full" value={tenantId} onChange={(event) => setTenantId(event.target.value)}><option value="all">All assignments</option><option value="unassigned">Unassigned</option>{tenants.map((tenant) => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}</select></label></div></div></details>;
}

function TenantManagementModal({ tenant, image, busy, imageBusy = false, onImageChange, onClose, onSave, onSaveAndValidate }: {
  tenant?: StoreDto;
  image?: string;
  busy: boolean;
  imageBusy?: boolean;
  onImageChange?: (file?: File) => Promise<void>;
  onClose: () => void;
  onSave: (input: Omit<StoreDto, 'id' | 'navigationNodeId' | 'storefrontImageUrl'>, imageFile?: File) => Promise<void>;
  onSaveAndValidate?: (input: Omit<StoreDto, 'id' | 'navigationNodeId' | 'storefrontImageUrl'>) => Promise<TenantSearchValidationResult>;
}) {
  const [name, setName] = useState(tenant?.name || '');
  const [description, setDescription] = useState(tenant?.description || '');
  const [category, setCategory] = useState(tenant?.category || 99);
  const [floorNumber, setFloorNumber] = useState(tenant?.floorNumber || 1);
  const [unitNumber, setUnitNumber] = useState(tenant?.unitNumber || '');
  const [contactEmail, setContactEmail] = useState(tenant?.contactEmail || '');
  const [contactPhone, setContactPhone] = useState(tenant?.contactPhone || '');
  const [openingHours, setOpeningHours] = useState(tenant?.openingHours || '');
  const [isActive, setIsActive] = useState(tenant?.isActive ?? true);
  const [pendingImage, setPendingImage] = useState<File | undefined>();
  const [pendingImagePreview, setPendingImagePreview] = useState('');
  const [imageError, setImageError] = useState('');
  const [aiBusy, setAiBusy] = useState(false);
  const [generateNewQuestions, setGenerateNewQuestions] = useState(false);
  const [aiError, setAiError] = useState('');
  const [validation, setValidation] = useState<TenantSearchValidationResult | null>(null);
  const [enrichment, setEnrichment] = useState<TenantDescriptionEnrichmentResult | null>(null);

  const validateSearch = async () => {
    if (!tenant) return;
    setAiBusy(true); setAiError(''); setValidation(null);
    try {
      setValidation(await validateTenantSearch(tenant.id, description, generateNewQuestions));
      setGenerateNewQuestions(false);
    }
    catch (error) { setAiError(error instanceof Error ? error.message : 'Search validation failed.'); }
    finally { setAiBusy(false); }
  };
  const enrichDescription = async () => {
    if (!tenant) return;
    setAiBusy(true); setAiError(''); setEnrichment(null);
    try { setEnrichment(await enrichTenantDescription(tenant.id, description)); }
    catch (error) { setAiError(error instanceof Error ? error.message : 'Description enrichment failed.'); }
    finally { setAiBusy(false); }
  };

  const selectImage = async (file?: File) => {
    if (!file) return;
    if (!file.type.startsWith('image/') || file.size > 10 * 1024 * 1024) {
      setImageError('Choose a PNG, JPEG, WebP or GIF image up to 10 MB.');
      return;
    }
    setImageError('');
    if (tenant && onImageChange) {
      await onImageChange(file);
      return;
    }
    setPendingImage(file);
    setPendingImagePreview(await fileToDataUrl(file));
  };

  const submit = () => onSave({
    name: name.trim(), description: description.trim(), category, floorNumber,
    unitNumber: unitNumber.trim(), contactEmail: contactEmail.trim() || null,
    contactPhone: contactPhone.trim() || null, openingHours: openingHours.trim() || null, isActive,
  }, pendingImage);
  const saveAndValidate = async () => {
    if (!onSaveAndValidate) return;
    setAiBusy(true); setAiError(''); setValidation(null);
    try {
      const result = await onSaveAndValidate({ name: name.trim(), description: description.trim(), category, floorNumber,
        unitNumber: unitNumber.trim(), contactEmail: contactEmail.trim() || null, contactPhone: contactPhone.trim() || null,
        openingHours: openingHours.trim() || null, isActive });
      setValidation(result);
    } catch (error) { setAiError(error instanceof Error ? error.message : 'Unable to save and validate tenant.'); }
    finally { setAiBusy(false); }
  };
  const displayedImage = pendingImagePreview || image;

  return <div className="modal modal-open"><div className="modal-box max-h-[92vh] w-[96vw] max-w-6xl overflow-y-auto">
    <button className="btn btn-circle btn-ghost btn-sm absolute right-4 top-4" aria-label="Close tenant management" onClick={onClose}><X size={16} /></button>
    <div className="pr-10"><h3 className="text-xl font-bold">{tenant ? 'Manage tenant' : 'Create tenant'}</h3><p className="mt-1 text-sm text-base-content/50">{tenant ? 'Update the directory information shoppers and mall staff use.' : 'Add a new tenant to the mall directory.'}</p></div>
    <div className="mt-7 grid gap-8 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
      <section className="space-y-5"><h4 className="text-sm font-bold uppercase tracking-wider text-base-content/45">Tenant details</h4>
        <div className="flex items-center gap-4 rounded-xl border border-base-300 bg-base-200/40 p-4"><div className="size-20 shrink-0 overflow-hidden rounded-xl border border-base-300 bg-gradient-to-br from-base-200 to-base-300">{displayedImage ? <img src={displayedImage} alt="" className="h-full w-full object-cover" /> : <span className="grid h-full place-items-center text-base-content/30"><Store size={24} /></span>}</div><div><div className="font-semibold">Storefront image</div><div className="mt-1 text-xs text-base-content/50">Shown on the public Directory card.</div><label className="btn btn-ghost btn-sm mt-3 cursor-pointer border border-base-300"><ImagePlus size={14} />{displayedImage ? 'Change image' : 'Add image'}<input hidden type="file" accept="image/png,image/jpeg,image/webp,image/gif" disabled={imageBusy} onChange={(event) => { void selectImage(event.target.files?.[0]); event.target.value = ''; }} /></label>{imageError && <div className="mt-2 text-xs text-error">{imageError}</div>}</div></div>
        <label className="form-control"><span className="label-text mb-1.5 font-semibold">Tenant name</span><input className="input input-bordered w-full" value={name} onChange={(event) => setName(event.target.value)} /></label>
        <div className="grid gap-x-4 gap-y-5 sm:grid-cols-2"><label className="form-control"><span className="label-text mb-2 font-semibold">Category</span><select className="select select-bordered w-full" value={category} onChange={(event) => setCategory(Number(event.target.value))}>{storeCategories.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        {tenant && <label className="form-control"><span className="label-text mb-1.5 font-semibold">Status</span><select className="select select-bordered w-full" value={isActive ? 'active' : 'inactive'} onChange={(event) => setIsActive(event.target.value === 'active')}><option value="active">Active</option><option value="inactive">Inactive</option></select></label>}
        <label className="form-control"><span className="label-text mb-1.5 font-semibold">Floor</span><input className="input input-bordered w-full" type="number" min={-5} max={50} value={floorNumber} onChange={(event) => setFloorNumber(Number(event.target.value))} /></label>
        <label className="form-control"><span className="label-text mb-1.5 font-semibold">Unit number</span><input className="input input-bordered w-full" value={unitNumber} onChange={(event) => setUnitNumber(event.target.value)} /></label>
        <label className="form-control"><span className="label-text mb-1.5 font-semibold">Contact email</span><input className="input input-bordered w-full" type="email" value={contactEmail} onChange={(event) => setContactEmail(event.target.value)} /></label>
        <label className="form-control"><span className="label-text mb-1.5 font-semibold">Contact phone</span><input className="input input-bordered w-full" value={contactPhone} onChange={(event) => setContactPhone(event.target.value)} /></label></div>
        <label className="form-control mt-1"><span className="label-text mb-2 font-semibold">Opening hours</span><input className="input input-bordered w-full" value={openingHours} onChange={(event) => setOpeningHours(event.target.value)} placeholder="Daily, 10:00 AM–10:00 PM" /></label>
      </section>
      <section className="min-w-0 space-y-5"><div className="flex flex-wrap items-center justify-between gap-3"><div><h4 className="text-sm font-bold uppercase tracking-wider text-base-content/45">Search description</h4><p className="mt-1 text-xs text-base-content/45">Use Markdown headings and lists to organize useful shopper details.</p></div>{tenant && <div className="flex flex-wrap gap-2"><button className="btn btn-primary btn-sm" disabled={aiBusy} onClick={() => void validateSearch()}><SearchCheck size={14} /> Validate search</button><button className="btn btn-secondary btn-sm" disabled={aiBusy} onClick={() => void enrichDescription()}><Sparkles size={14} /> Enrich</button></div>}</div>{tenant && <label className="flex cursor-pointer items-start gap-2 rounded-lg border border-base-300 bg-base-100 px-3 py-2.5 text-xs"><input className="checkbox checkbox-sm checkbox-primary mt-0.5" type="checkbox" checked={generateNewQuestions} disabled={aiBusy} onChange={(event) => setGenerateNewQuestions(event.target.checked)} /><span><span className="font-semibold">Generate new search questions</span><span className="mt-0.5 block text-base-content/50">When unchecked, validation reuses this tenant’s saved questions if available.</span></span></label>}<MarkdownEditor value={description} onChange={(value) => { setDescription(value); setValidation(null); setEnrichment(null); }} title="" onTitleChange={() => {}} hideDocumentHeader compact maxLength={1500} placeholder="Describe what shoppers can find here…" /><div className="text-right text-xs text-base-content/40">{description.length}/1500</div>
        {aiError && <div className="alert alert-error py-2 text-sm">{aiError}</div>}
        {enrichment && <div className="rounded-xl border border-primary/30 bg-primary/5 p-4"><div className="flex items-center gap-2 font-semibold"><Sparkles size={15} /> Enriched Markdown draft</div><div className="mt-2 rounded-lg bg-base-100 p-3 text-sm"><MarkdownDescription markdown={enrichment.description} /></div>{enrichment.changes.length > 0 && <ul className="mt-2 list-inside list-disc text-xs text-base-content/60">{enrichment.changes.map((change, index) => <li key={index}>{change}</li>)}</ul>}<div className="mt-3 flex gap-2"><button className="btn btn-primary btn-xs" disabled={enrichment.description.length > 1500} onClick={() => { setDescription(enrichment.description); setEnrichment(null); }}>Apply draft</button><button className="btn btn-ghost btn-xs" onClick={() => setEnrichment(null)}>Discard</button></div></div>}
        {validation && <div className="rounded-xl border border-base-300 bg-base-200/40 p-4"><div className="flex items-center gap-2 font-semibold"><span className="flex text-warning">{[1,2,3,4,5].map((star) => <Star key={star} size={15} fill={star <= validation.rating ? 'currentColor' : 'none'} />)}</span><span>{validation.rating}/5 search rating</span></div><p className="mt-2 text-sm">{validation.summary}</p><div className="mt-3 grid gap-2">{validation.questions.map((item, index) => <div key={`${item.question}-${index}`} className="rounded-lg bg-base-100 p-3"><div className="text-sm font-medium">“{item.question}”</div><div className="mt-1 text-xs text-base-content/60">{item.tenantRank ? `Ranked #${item.tenantRank}` : 'Not in top 5'} · {item.isRelevant ? 'Relevant' : 'Needs improvement'} — {item.assessment}</div></div>)}</div></div>}
      </section>
    </div>
    <div className="modal-action"><button className="btn btn-ghost" disabled={busy || aiBusy} onClick={onClose}>Cancel</button>{tenant && onSaveAndValidate && <button className="btn btn-secondary" disabled={busy || aiBusy || name.trim().length < 2 || !unitNumber.trim()} onClick={() => void saveAndValidate()}>{aiBusy ? <span className="loading loading-spinner loading-sm" /> : <><Save size={15} /> Save &amp; validate</>}</button>}<button className="btn btn-primary" disabled={busy || aiBusy || name.trim().length < 2 || !unitNumber.trim()} onClick={() => void submit()}>{busy ? <span className="loading loading-spinner loading-sm" /> : tenant ? <Edit3 size={15} /> : <Plus size={15} />} {tenant ? 'Save tenant' : 'Create tenant'}</button></div>
  </div><button className="modal-backdrop" onClick={onClose}>close</button></div>;
}

function CreateUserModal({ tenants, busy, onClose, onSave }: {
  tenants: StoreDto[];
  busy: boolean;
  onClose: () => void;
  onSave: (input: {
    fullName: string;
    email: string;
    password: string;
    role: Exclude<UserRole, 'Guest'>;
    tenantId: string | null;
  }) => Promise<void>;
}) {
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<Exclude<UserRole, 'Guest'>>('Shopper');
  const [tenantId, setTenantId] = useState(tenants[0]?.id || '');
  const needsTenant = role === 'TenantStaff';
  const canSubmit = fullName.trim().length >= 2 && email.trim().length > 0 && password.length >= 8 && (!needsTenant || tenantId);

  return <div className="modal modal-open"><div className="modal-box max-w-xl">
    <button className="btn btn-circle btn-ghost btn-sm absolute right-4 top-4" aria-label="Close user creation" onClick={onClose}><X size={16} /></button>
    <div className="pr-10"><h3 className="text-xl font-bold">Create user</h3><p className="mt-1 text-sm text-base-content/50">Add a password-backed account and assign its access.</p></div>
    <div className="mt-6 grid gap-4">
      <label className="form-control"><span className="label-text mb-1.5 font-semibold">Full name</span><input className="input input-bordered w-full" autoComplete="name" value={fullName} onChange={(event) => setFullName(event.target.value)} /></label>
      <label className="form-control"><span className="label-text mb-1.5 font-semibold">Email</span><input className="input input-bordered w-full" type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} /></label>
      <label className="form-control"><span className="label-text mb-1.5 font-semibold">Temporary password</span><input className="input input-bordered w-full" type="password" minLength={8} autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} /><span className="mt-1 text-xs text-base-content/45">Use at least 8 characters. Share it with the user securely.</span></label>
      <label className="form-control"><span className="label-text mb-1.5 font-semibold">Role</span><select className="select select-bordered w-full" value={role} onChange={(event) => setRole(event.target.value as Exclude<UserRole, 'Guest'>)}><option value="Shopper">Shopper</option><option value="TenantStaff">Tenant staff</option><option value="Admin">Administrator</option></select></label>
      {needsTenant && <label className="form-control"><span className="label-text mb-1.5 font-semibold">Tenant assignment</span><select className="select select-bordered w-full" value={tenantId} onChange={(event) => setTenantId(event.target.value)}><option value="" disabled>Select a tenant</option>{tenants.map((tenant) => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}</select></label>}
    </div>
    <div className="modal-action"><button className="btn btn-ghost" disabled={busy} onClick={onClose}>Cancel</button><button className="btn btn-primary" disabled={busy || !canSubmit} onClick={() => void onSave({ fullName: fullName.trim(), email: email.trim(), password, role, tenantId: needsTenant ? tenantId : null })}>{busy ? <span className="loading loading-spinner loading-sm" /> : <Plus size={15} />} Create user</button></div>
  </div><button className="modal-backdrop" onClick={onClose}>close</button></div>;
}

function UserManagementModal({ user, tenants, busy, onClose, onSave }: {
  user: ManagedUserDto;
  tenants: StoreDto[];
  busy: boolean;
  onClose: () => void;
  onSave: (role: Exclude<UserRole, 'Guest'>, tenantId: string | null) => Promise<void>;
}) {
  const [role, setRole] = useState<Exclude<UserRole, 'Guest'>>(user.role as Exclude<UserRole, 'Guest'>);
  const [tenantId, setTenantId] = useState(user.tenantId || tenants[0]?.id || '');
  const needsTenant = role === 'TenantStaff';
  return <div className="modal modal-open"><div className="modal-box max-w-xl">
    <button className="btn btn-circle btn-ghost btn-sm absolute right-4 top-4" aria-label="Close user management" onClick={onClose}><X size={16} /></button>
    <div className="pr-10"><h3 className="text-xl font-bold">Manage user access</h3><p className="mt-1 text-sm text-base-content/50">Change the account role and tenant assignment.</p></div>
    <div className="mt-6 rounded-xl bg-base-200 p-4"><div className="font-semibold">{user.fullName}</div><div className="mt-1 text-xs text-base-content/50">{user.email || user.userName}</div></div>
    <div className="mt-5 grid gap-4">
      <label className="form-control"><span className="label-text mb-1.5 font-semibold">Role</span><select className="select select-bordered w-full" value={role} onChange={(event) => setRole(event.target.value as Exclude<UserRole, 'Guest'>)}><option value="Shopper">Shopper</option><option value="TenantStaff">Tenant staff</option><option value="Admin">Administrator</option></select></label>
      {needsTenant && <label className="form-control"><span className="label-text mb-1.5 font-semibold">Tenant assignment</span><select className="select select-bordered w-full" value={tenantId} onChange={(event) => setTenantId(event.target.value)}><option value="" disabled>Select a tenant</option>{tenants.map((tenant) => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}</select></label>}
    </div>
    <div className="modal-action"><button className="btn btn-ghost" disabled={busy} onClick={onClose}>Cancel</button><button className="btn btn-primary" disabled={busy || (needsTenant && !tenantId)} onClick={() => void onSave(role, needsTenant ? tenantId : null)}>{busy ? <span className="loading loading-spinner loading-sm" /> : <Edit3 size={15} />} Save access</button></div>
  </div><button className="modal-backdrop" onClick={onClose}>close</button></div>;
}
