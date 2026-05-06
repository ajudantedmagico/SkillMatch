const API_URL = 'http://localhost:5000/api';

const api = {
  // ── Token ──
  getToken: () => localStorage.getItem('sm_token'),
  setToken: (t) => localStorage.setItem('sm_token', t),
  clearToken: () => { localStorage.removeItem('sm_token'); localStorage.removeItem('sm_user'); },

  getUser: () => JSON.parse(localStorage.getItem('sm_user') || 'null'),
  setUser: (u) => localStorage.setItem('sm_user', JSON.stringify(u)),

  isLoggedIn: () => !!localStorage.getItem('sm_token'),

  // ── Fetch base ──
  async _fetch(path, options = {}) {
    const token = this.getToken();
    const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
    if (token) headers['Authorization'] = `Bearer ${token}`;

    const res = await fetch(`${API_URL}${path}`, { ...options, headers });

    if (res.status === 401) {
      this.clearToken();
      window.location.href = '/index.html';
      return;
    }

    if (res.status === 204) return null;

    const data = await res.json().catch(() => null);
    if (!res.ok) throw new Error(data?.message || `Erro ${res.status}`);
    return data;
  },

  // ── Auth ──
  async register(nome, email, senha) {
    const data = await this._fetch('/auth/register', {
      method: 'POST', body: JSON.stringify({ nome, email, senha })
    });
    this.setToken(data.token);
    this.setUser({ id: data.usuarioId, nome: data.nome, email });
    return data;
  },

  async login(email, senha) {
    const data = await this._fetch('/auth/login', {
      method: 'POST', body: JSON.stringify({ email, senha })
    });
    this.setToken(data.token);
    this.setUser({ id: data.usuarioId, nome: data.nome, email });
    return data;
  },

  logout() {
    this.clearToken();
    window.location.href = 'index.html';
  },

  async changePassword(senhaAtual, novaSenha) {
    const data = await this._fetch('/auth/change-password', {
      method: 'PUT',
      body: JSON.stringify({ senhaAtual, novaSenha })
    });
    return data;
  },

  async deleteAccount(senha) {
    const data = await this._fetch('/auth/account', {
      method: 'DELETE',
      body: JSON.stringify({ senha })
    });
    return data;
  },

  // ── Perfil Âncora ──
  getPerfil: () => api._fetch('/perfil'),
  salvarPerfil: (dto) => api._fetch('/perfil', { method: 'PUT', body: JSON.stringify(dto) }),

  // ── Currículo ──
  gerarCV: (descricaoVaga, consentimentoIA) =>
    api._fetch('/curriculos/gerar', {
      method: 'POST',
      body: JSON.stringify({ descricaoVaga, consentimentoIA })
    }),

  salvarCV: (payload) =>
    api._fetch('/curriculos/salvar', {
      method: 'POST',
      body: JSON.stringify(payload)
    }),

  listarCVs: () => api._fetch('/curriculos'),
  getCV: (id) => api._fetch(`/curriculos/${id}`),
};

// ── Utilidades de UI ──
function showToast(msg, type = 'success') {
  document.querySelectorAll('.toast').forEach(t => t.remove());
  const t = document.createElement('div');
  t.className = `toast ${type}`;
  t.textContent = msg;
  document.body.appendChild(t);
  setTimeout(() => t.remove(), 3500);
}

function showLoading(msg = 'Processando...') {
  const el = document.createElement('div');
  el.className = 'loading-overlay';
  el.id = 'loading-overlay';
  el.innerHTML = `<div class="spinner"></div><p>${msg}</p>`;
  document.body.appendChild(el);
}

function hideLoading() {
  document.getElementById('loading-overlay')?.remove();
}

function requireAuth() {
  if (!api.isLoggedIn()) window.location.href = '/index.html';
}

function formatDate(dateStr) {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleDateString('pt-BR', { month: '2-digit', year: 'numeric' });
}
