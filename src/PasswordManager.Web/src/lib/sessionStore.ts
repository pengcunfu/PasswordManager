const STORAGE_KEY = 'pm.session'

export type StoredAccount = {
  username: string
  userId: string
  accessToken: string
  keyRaw: string
}

type StoredState = {
  version: 2
  activeUserId: string | null
  accounts: StoredAccount[]
}

function emptyState(): StoredState {
  return { version: 2, activeUserId: null, accounts: [] }
}

export function loadState(): StoredState {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return emptyState()
    const data = JSON.parse(raw) as StoredState & StoredAccount
    if (data.version === 2 && Array.isArray(data.accounts)) {
      return {
        version: 2,
        activeUserId: data.activeUserId ?? data.accounts[0]?.userId ?? null,
        accounts: data.accounts.filter((a) => a.userId && a.username && a.keyRaw),
      }
    }
    if (data.userId && data.username && data.keyRaw) {
      return {
        version: 2,
        activeUserId: data.userId,
        accounts: [{
          username: data.username,
          userId: data.userId,
          accessToken: data.accessToken || '',
          keyRaw: data.keyRaw,
        }],
      }
    }
    return emptyState()
  } catch {
    return emptyState()
  }
}

function saveState(state: StoredState) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

export function loadStoredSession(): StoredAccount | null {
  const state = loadState()
  if (!state.activeUserId) return null
  return state.accounts.find((a) => a.userId === state.activeUserId) ?? null
}

export function listStoredAccounts(): StoredAccount[] {
  return loadState().accounts
}

export function getActiveUserId(): string | null {
  return loadState().activeUserId
}

export function saveStoredSession(account: StoredAccount) {
  const state = loadState()
  const rest = state.accounts.filter((a) => a.userId !== account.userId)
  saveState({
    version: 2,
    activeUserId: account.userId,
    accounts: [...rest, account],
  })
}

export function setActiveUserId(userId: string) {
  const state = loadState()
  if (!state.accounts.some((a) => a.userId === userId)) return
  saveState({ ...state, activeUserId: userId })
}

export function updateStoredToken(accessToken: string) {
  const state = loadState()
  if (!state.activeUserId) return
  saveState({
    ...state,
    accounts: state.accounts.map((a) =>
      a.userId === state.activeUserId ? { ...a, accessToken } : a,
    ),
  })
}

export function removeStoredAccount(userId: string) {
  const state = loadState()
  const accounts = state.accounts.filter((a) => a.userId !== userId)
  const activeUserId = state.activeUserId === userId
    ? (accounts[0]?.userId ?? null)
    : state.activeUserId
  saveState({ version: 2, activeUserId, accounts })
}

export function clearStoredSession() {
  sessionStorage.removeItem(STORAGE_KEY)
}

export function hasStoredSession() {
  return loadStoredSession() !== null
}
