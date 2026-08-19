import type { AboutInfo, AuthResponse, Settings, VaultDoc } from '../types'
import { getActiveUserId, updateStoredToken } from './sessionStore'

let accessToken = ''

export function setAccessToken(token: string) {
  accessToken = token
  if (token) updateStoredToken(token)
}

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  let res: Response
  try {
    res = await fetch(path, { ...init, headers, credentials: 'include' })
  } catch {
    throw new ApiError(0, '无法连接后端，请先运行 scripts\\run-api.cmd')
  }

  if (res.status === 401 && retry && !path.startsWith('/api/auth/')) {
    const ok = await tryRefresh()
    if (ok) return request<T>(path, init, false)
  }

  if (res.status === 204) return undefined as T

  const text = await res.text()
  let data: { error?: string } | null = null
  if (text) {
    try {
      data = JSON.parse(text)
    } catch {
      data = null
    }
  }
  if (!res.ok) {
    const offline = res.status === 502 || res.status === 503 || res.status === 504
    throw new ApiError(
      res.status,
      offline
        ? '后端 API 未启动，请先运行 scripts\\run-api.cmd'
        : (data?.error || res.statusText || '请求失败'),
    )
  }
  if (!text) return undefined as T
  if (!data) throw new ApiError(res.status, '服务器返回了无法解析的响应')
  return data as T
}

async function tryRefresh(): Promise<boolean> {
  try {
    const data = await request<AuthResponse>('/api/auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ userId: getActiveUserId() }),
    }, false)
    setAccessToken(data.accessToken)
    return true
  } catch {
    return false
  }
}

export const api = {
  register: (username: string, password: string, kdfSalt: string) =>
    request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username, password, kdfSalt }),
    }),

  login: (username: string, password: string) =>
    request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    }),

  logout: (userId?: string | null, all = false) =>
    request<void>('/api/auth/logout', {
      method: 'POST',
      body: JSON.stringify({ userId: userId || undefined, all }),
    }, false),

  refresh: (userId?: string | null) =>
    request<AuthResponse>('/api/auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ userId: userId || undefined }),
    }, false),

  me: () => request<{ userId: string; username: string }>('/api/auth/me'),

  getVault: () => request<{ document: VaultDoc; updatedAt: string }>('/api/vault'),

  saveVault: (document: VaultDoc) =>
    request<{ document: VaultDoc; updatedAt: string }>('/api/vault', {
      method: 'PUT',
      body: JSON.stringify({ document }),
    }),

  backup: () => request<unknown>('/api/vault/backup'),

  getSettings: () => request<Settings>('/api/settings'),

  saveSettings: (body: unknown) =>
    request<Settings>('/api/settings', { method: 'PUT', body: JSON.stringify(body) }),

  about: () => request<AboutInfo>('/api/vault/about'),

  testAi: (body: unknown) =>
    request<{ success: boolean; error?: string }>('/api/ai/test', {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  aiCompletions: async (body: unknown, signal?: AbortSignal) => {
    const headers = new Headers({ 'Content-Type': 'application/json' })
    if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
    const res = await fetch('/api/ai/completions', {
      method: 'POST',
      headers,
      credentials: 'include',
      body: JSON.stringify(body),
      signal,
    })
    if (!res.ok) {
      const text = await res.text()
      let message = 'AI 请求失败'
      try {
        message = JSON.parse(text).error || message
      } catch {
        /* ignore */
      }
      throw new ApiError(res.status, message)
    }
    return res
  },
}
