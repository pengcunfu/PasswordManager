const STORAGE_KEY = 'pm.session'

export type StoredSession = {
  username: string
  userId: string
  accessToken: string
  keyRaw: string
}

export function loadStoredSession(): StoredSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const data = JSON.parse(raw) as StoredSession
    if (!data.username || !data.userId || !data.keyRaw) return null
    return data
  } catch {
    return null
  }
}

export function saveStoredSession(session: StoredSession) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
}

export function updateStoredToken(accessToken: string) {
  const current = loadStoredSession()
  if (!current) return
  saveStoredSession({ ...current, accessToken })
}

export function clearStoredSession() {
  sessionStorage.removeItem(STORAGE_KEY)
}

export function hasStoredSession() {
  return loadStoredSession() !== null
}
