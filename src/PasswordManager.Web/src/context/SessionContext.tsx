import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setAccessToken } from '../lib/api'
import { deriveKey, exportKeyRaw, generateSalt, importKeyRaw } from '../lib/crypto'
import {
  clearStoredSession,
  hasStoredSession,
  loadStoredSession,
  saveStoredSession,
} from '../lib/sessionStore'

type Session = {
  username: string
  userId: string
  key: CryptoKey
}

type SessionContextValue = {
  session: Session | null
  ready: boolean
  login: (username: string, password: string) => Promise<void>
  register: (username: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const SessionContext = createContext<SessionContextValue | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null)
  const [ready, setReady] = useState(!hasStoredSession())

  useEffect(() => {
    let cancelled = false

    async function restore() {
      const stored = loadStoredSession()
      if (!stored) {
        setReady(true)
        return
      }

      try {
        const key = await importKeyRaw(stored.keyRaw)
        setAccessToken(stored.accessToken)

        try {
          const refreshed = await api.refresh()
          setAccessToken(refreshed.accessToken)
          if (!cancelled) {
            setSession({ username: refreshed.username, userId: refreshed.userId, key })
          }
        } catch {
          const me = await api.me()
          if (!cancelled) {
            setSession({ username: me.username, userId: me.userId, key })
          }
        }
      } catch {
        clearStoredSession()
        setAccessToken('')
        if (!cancelled) setSession(null)
      } finally {
        if (!cancelled) setReady(true)
      }
    }

    void restore()
    return () => {
      cancelled = true
    }
  }, [])

  const value = useMemo<SessionContextValue>(
    () => ({
      session,
      ready,
      login: async (username, password) => {
        const res = await api.login(username.trim(), password)
        await openSession(res.accessToken, res.username, res.userId, await deriveKey(password, res.kdfSalt))
      },
      register: async (username, password) => {
        const kdfSalt = generateSalt()
        const res = await api.register(username.trim(), password, kdfSalt)
        await openSession(res.accessToken, res.username, res.userId, await deriveKey(password, res.kdfSalt))
      },
      logout: async () => {
        try {
          await api.logout()
        } finally {
          clearStoredSession()
          setAccessToken('')
          setSession(null)
        }
      },
    }),
    [session, ready],
  )

  async function openSession(accessToken: string, username: string, userId: string, key: CryptoKey) {
    setAccessToken(accessToken)
    saveStoredSession({
      username,
      userId,
      accessToken,
      keyRaw: await exportKeyRaw(key),
    })
    setSession({ username, userId, key })
  }

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

export function useSession() {
  const ctx = useContext(SessionContext)
  if (!ctx) throw new Error('SessionProvider missing')
  return ctx
}
