import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { homePathForUser, useAuth } from '../auth/AuthContext';

const DEMO_USERS = [
  { email: 'fleet@demo.local', label: 'General fleet' },
  { email: 'av@demo.local', label: 'Robo-taxi' },
  { email: 'delivery@demo.local', label: 'Delivery' },
  { email: 'admin@demo.local', label: 'Platform admin' },
];

export function LoginPage() {
  const { login, token, user } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('fleet@demo.local');
  const [password, setPassword] = useState('Demo123!');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  if (token && user) {
    return <Navigate to={homePathForUser(user)} replace />;
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      const profile = await login(email, password);
      navigate(homePathForUser(profile));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={submit}>
        <h1>UniConnect</h1>
        <p className="muted">Sign in to your fleet workspace</p>
        {error && <p className="error">{error}</p>}
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </label>
        <button type="submit" disabled={submitting}>{submitting ? 'Signing in…' : 'Sign in'}</button>
        <div className="demo-users">
          <p className="muted">Demo accounts (password: Demo123!)</p>
          {DEMO_USERS.map((u) => (
            <button key={u.email} type="button" className="secondary" onClick={() => setEmail(u.email)}>
              {u.label}
            </button>
          ))}
        </div>
      </form>
    </div>
  );
}
