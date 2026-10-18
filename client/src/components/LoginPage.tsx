import { useState } from 'react';
import { ArrowLeft, Building2, CheckCircle2, Navigation, ShoppingBag, Sparkles } from 'lucide-react';
import { getCurrentUser, loginUser, registerShopper } from '../api';
import { mallConfig } from '../mallConfig';
import type { UserProfileDto } from '../types';

interface LoginPageProps {
  onAuthenticated: (profile: UserProfileDto) => void;
  onBack: () => void;
}

export function LoginPage({ onAuthenticated, onBack }: LoginPageProps) {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState<string>(mallConfig.adminLogin);
  const [password, setPassword] = useState<string>(mallConfig.adminPassword);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const finishAuthentication = async () => {
    onAuthenticated(await getCurrentUser());
  };

  const submitLogin = async (event: React.FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      if (mode === 'register') await registerShopper(fullName, email, password);
      else await loginUser(email, password);
      await finishAuthentication();
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to authenticate.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div data-theme="light" className="mall-shell min-h-screen p-4 text-base-content md:p-6">
      <button className="btn btn-ghost btn-sm rounded-full" onClick={onBack}><ArrowLeft size={15} /> Back to the mall</button>
      <main className="mx-auto grid min-h-[86vh] max-w-6xl items-stretch overflow-hidden rounded-3xl border border-base-300 bg-base-100 shadow-xl shadow-slate-300/20 lg:grid-cols-[1.05fr_.95fr]">
        <section className="relative hidden overflow-hidden bg-primary p-12 text-primary-content lg:flex lg:flex-col lg:justify-between">
          <div className="relative z-10 flex items-center gap-3"><span className="grid size-11 place-items-center rounded-xl bg-white/20 backdrop-blur"><Building2 size={21} /></span><strong className="block text-xl tracking-tight">{mallConfig.platformName}</strong></div>
          <div className="relative z-10 max-w-lg"><span className="inline-flex rounded-full bg-white/15 px-3 py-1 text-xs font-bold uppercase tracking-wider backdrop-blur">Your Kluang visit, remembered</span><h1 className="mt-5 text-5xl font-bold leading-[1.02] tracking-[-0.055em]">Everything happening at Sunway Kluang, in one place.</h1><p className="mt-5 max-w-md leading-7 opacity-80">Sign in to discover the mall directory, current deals, events and visitor information for {mallConfig.name}.</p><div className="mt-8 grid gap-3 text-sm"><span className="flex items-center gap-3"><Sparkles size={17} /> Personalized discovery</span><span className="flex items-center gap-3"><ShoppingBag size={17} /> Current deals and stores</span><span className="flex items-center gap-3"><Navigation size={17} /> Mall visit assistance</span></div></div>
          <p className="relative z-10 flex items-center gap-2 text-xs opacity-70"><CheckCircle2 size={14} /> Guests can still browse automatically without signing in.</p>
          <div className="absolute -right-24 -top-20 size-96 rounded-full border-[65px] border-white/10" />
        </section>

        <section className="flex items-center p-6 sm:p-10 lg:p-14">
          <form className="mx-auto w-full max-w-md" onSubmit={submitLogin}>
            <div className="mb-8 lg:hidden"><span className="mb-4 grid size-11 place-items-center rounded-xl bg-primary text-primary-content"><Building2 size={21} /></span><strong className="text-xl">{mallConfig.platformName}</strong></div>
            <h2 className="text-3xl font-bold tracking-[-0.04em]">{mode === 'login' ? 'Welcome back' : 'Create your account'}</h2><p className="mt-2 text-sm leading-6 text-base-content/55">{mode === 'login' ? `Sign in to continue to ${mallConfig.platformName}.` : 'Register to save your preferences across visits.'}</p>

            <div className="my-7 flex rounded-full bg-base-200 p-1 text-sm"><button type="button" className={`flex-1 rounded-full py-2.5 font-semibold transition ${mode === 'login' ? 'bg-base-100 shadow-sm' : 'text-base-content/50'}`} onClick={() => { setMode('login'); setError(''); }}>Log in</button><button type="button" className={`flex-1 rounded-full py-2.5 font-semibold transition ${mode === 'register' ? 'bg-base-100 shadow-sm' : 'text-base-content/50'}`} onClick={() => { setMode('register'); setEmail(''); setPassword(''); setError(''); }}>Register</button></div>
            {error && <div className="alert alert-error mb-5 py-3 text-sm">{error}</div>}
            <div className="grid gap-5">
              {mode === 'register' && <label className="form-control gap-2"><span className="label-text text-sm font-semibold">Full name</span><input className="input input-bordered h-12 w-full rounded-xl" type="text" autoComplete="name" value={fullName} onChange={(event) => setFullName(event.target.value)} minLength={2} required /></label>}
              <label className="form-control gap-2"><span className="label-text text-sm font-semibold">Email address</span><input className="input input-bordered h-12 w-full rounded-xl" type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></label>
              <label className="form-control gap-2"><span className="label-text text-sm font-semibold">Password</span><input className="input input-bordered h-12 w-full rounded-xl" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} value={password} onChange={(event) => setPassword(event.target.value)} minLength={mode === 'register' ? 8 : undefined} required /></label>
              <button className="btn btn-primary mt-2 h-12 w-full rounded-full" disabled={submitting}>{submitting ? <span className="loading loading-spinner loading-sm" /> : mode === 'login' ? 'Log in' : 'Create shopper account'}</button>
            </div>
            <details className="mt-7 border-t border-base-200 pt-5 text-xs text-base-content/50"><summary className="cursor-pointer font-semibold">Development access credentials</summary><p className="mt-3"><strong>Admin:</strong> {mallConfig.adminLogin} / {mallConfig.adminPassword}</p><p className="mt-1"><strong>Tenant:</strong> {mallConfig.tenantLogin} / {mallConfig.tenantPassword}</p></details>
          </form>
        </section>
      </main>
    </div>
  );
}
