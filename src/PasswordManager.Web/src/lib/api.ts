import type { AboutInfo, AuthResponse, Entry, Group, Settings } from '../types'
import { updateStoredToken } from './sessionStore'

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

  const res = await fetch(path, { ...init, headers, credentials: 'include' })

  if (res.status === 401 && retry && !path.startsWith('/api/auth/')) {
    const ok = await tryRefresh()
    if (ok) return request<T>(path, init, false)
  }

  if (res.status === 204) return undefined as T

  const text = await res.text()
  const data = text ? JSON.parse(text) : null
  if (!res.ok) {
    throw new ApiError(res.status, data?.error || res.statusText || '请求失败')
  }
  return data as T
}

async function tryRefresh(): Promise<boolean> {
  try {
    const data = await request<AuthResponse>('/api/auth/refresh', { method: 'POST' }, false)
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

  logout: () => request<void>('/api/auth/logout', { method: 'POST' }, false),

  refresh: () =>
    request<AuthResponse>('/api/auth/refresh', { method: 'POST' }, false),

  me: () => request<{ userId: string; username: string }>('/api/auth/me'),

  listEntries: (keyword?: string, groupId?: string | null) => {
    const q = new URLSearchParams()
    if (keyword) q.set('keyword', keyword)
    if (groupId) q.set('groupId', groupId)
    const qs = q.toString()
    return request<Entry[]>(`/api/entries${qs ? `?${qs}` : ''}`)
  },

  getEntry: (id: string) => request<Entry>(`/api/entries/${id}`),

  createEntry: (body: unknown) =>
    request<Entry>('/api/entries', { method: 'POST', body: JSON.stringify(body) }),

  updateEntry: (id: string, body: unknown) =>
    request<Entry>(`/api/entries/${id}`, { method: 'PUT', body: JSON.stringify(body) }),

  deleteEntry: (id: string) =>
    request<void>(`/api/entries/${id}`, { method: 'DELETE' }),

  listGroups: () => request<Group[]>('/api/groups'),

  createGroup: (body: unknown) =>
    request<Group>('/api/groups', { method: 'POST', body: JSON.stringify(body) }),

  updateGroup: (id: string, body: unknown) =>
    request<Group>(`/api/groups/${id}`, { method: 'PUT', body: JSON.stringify(body) }),

  deleteGroup: (id: string) =>
    request<void>(`/api/groups/${id}`, { method: 'DELETE' }),

  getSettings: () => request<Settings>('/api/settings'),

  saveSettings: (body: unknown) =>
    request<Settings>('/api/settings', { method: 'PUT', body: JSON.stringify(body) }),

  backup: () => request<unknown>('/api/vault/backup'),

  importVault: (body: unknown) =>
    request<{ groupsCreated: number; entriesImported: number; entriesSkipped: number }>(
      '/api/vault/import',
      { method: 'POST', body: JSON.stringify(body) },
    ),

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
