import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setAccessToken } from '../lib/api'
import { deriveKey, exportKeyRaw, generateSalt, importKeyRaw } from '../lib/crypto'
import {
  clearStoredSession,
  getActiveUserId,
  hasStoredSession,
  listStoredAccounts,
  loadStoredSession,
  removeStoredAccount,
  saveStoredSession,
  setActiveUserId,
  type StoredAccount,
} from '../lib/sessionStore'

export type Session = {
  username: string
  userId: string
  key: CryptoKey
}

export type AccountInfo = {
  username: string
  userId: string
}

type SessionContextValue = {
  session: Session | null
  accounts: AccountInfo[]
  ready: boolean
  addingAccount: boolean
  login: (username: string, password: string) => Promise<void>
  register: (username: string, password: string) => Promise<void>
  switchAccount: (userId: string) => Promise<void>
  startAddAccount: () => void
  cancelAddAccount: () => void
  logout: () => Promise<void>
  logoutAll: () => Promise<void>
}

const SessionContext = createContext<SessionContextValue | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null)
  const [accounts, setAccounts] = useState<AccountInfo[]>(() => publicAccounts())
  const [ready, setReady] = useState(!hasStoredSession())
  const [addingAccount, setAddingAccount] = useState(false)

  const refreshAccounts = useCallback(() => {
    setAccounts(publicAccounts())
  }, [])

  const openSession = useCallback(async (
    accessToken: string,
    username: string,
    userId: string,
    key: CryptoKey,
  ) => {
    setAccessToken(accessToken)
    saveStoredSession({
      username,
      userId,
      accessToken,
      keyRaw: await exportKeyRaw(key),
    })
    setSession({ username, userId, key })
    setAccounts(publicAccounts())
    setAddingAccount(false)
  }, [])

  const switchAccount = useCallback(async (userId: string) => {
    const stored = listStoredAccounts().find((a) => a.userId === userId)
    if (!stored) throw new Error('该账号未在本会话解锁')
    const previous = getActiveUserId()
    setActiveUserId(userId)
    setAccessToken(stored.accessToken)
    const key = await importKeyRaw(stored.keyRaw)
    try {
      const refreshed = await api.refresh(userId)
      setAccessToken(refreshed.accessToken)
      setSession({ username: refreshed.username, userId: refreshed.userId, key })
    } catch {
      try {
        const me = await api.me()
        setSession({ username: me.username, userId: me.userId, key })
      } catch {
        if (previous) setActiveUserId(previous)
        throw new Error('该账号会话已过期，请重新输入主密码')
      }
    }
    setAddingAccount(false)
    setAccounts(publicAccounts())
  }, [])

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
          const refreshed = await api.refresh(stored.userId)
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
        if (!cancelled) refreshAccounts()
      } catch {
        removeStoredAccount(stored.userId)
        setAccessToken('')
        if (!cancelled) {
          setSession(null)
          refreshAccounts()
        }
      } finally {
        if (!cancelled) setReady(true)
      }
    }

    void restore()
    return () => {
      cancelled = true
    }
  }, [refreshAccounts])

  const login = useCallback(async (username: string, password: string) => {
    const res = await api.login(username.trim(), password)
    await openSession(res.accessToken, res.username, res.userId, await deriveKey(password, res.kdfSalt))
  }, [openSession])

  const register = useCallback(async (username: string, password: string) => {
    const kdfSalt = generateSalt()
    const res = await api.register(username.trim(), password, kdfSalt)
    await openSession(res.accessToken, res.username, res.userId, await deriveKey(password, res.kdfSalt))
  }, [openSession])

  const logout = useCallback(async () => {
    const currentId = session?.userId
    try {
      await api.logout(currentId)
    } catch {
      /* ignore */
    }
    if (currentId) removeStoredAccount(currentId)
    refreshAccounts()
    const remaining = listStoredAccounts()
    if (remaining[0]) {
      try {
        await switchAccount(remaining[0].userId)
        return
      } catch {
        setAccessToken('')
        setSession(null)
        return
      }
    }
    setAccessToken('')
    setSession(null)
  }, [session?.userId, refreshAccounts, switchAccount])

  const logoutAll = useCallback(async () => {
    try {
      await api.logout(undefined, true)
    } finally {
      clearStoredSession()
      setAccessToken('')
      setSession(null)
      setAccounts([])
      setAddingAccount(false)
    }
  }, [])

  const value = useMemo<SessionContextValue>(
    () => ({
      session,
      accounts,
      ready,
      addingAccount,
      login,
      register,
      switchAccount,
      startAddAccount: () => setAddingAccount(true),
      cancelAddAccount: () => setAddingAccount(false),
      logout,
      logoutAll,
    }),
    [session, accounts, ready, addingAccount, login, register, switchAccount, logout, logoutAll],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

function publicAccounts(): AccountInfo[] {
  return listStoredAccounts().map((a: StoredAccount) => ({
    username: a.username,
    userId: a.userId,
  }))
}

export function useSession() {
  const ctx = useContext(SessionContext)
  if (!ctx) throw new Error('SessionProvider missing')
  return ctx
}
