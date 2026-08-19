export type CustomField = {
  key: string
  value: string
  isHidden: boolean
}

export type ItemType = 'login' | 'credential' | 'key' | 'note'

export type Account = {
  id: string
  label: string
  username: string
  secret: string
  notes: string
  fields: CustomField[]
}

export type VaultItem = {
  id: string
  type: ItemType
  title: string
  url: string
  groupId: string | null
  category: string
  notes: string
  accounts: Account[]
  createdAt: string
  updatedAt: string
}

export type Group = {
  id: string
  name: string
  description: string
  color: string
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export type VaultDoc = {
  version: string
  groups: Group[]
  items: VaultItem[]
}

export type Settings = {
  theme: string
  autoLockMinutes: number
  clearClipboardSeconds: number
  aiApiEndpoint: string
  aiApiKey: string
  aiModel: string
  aiMaxTokens: number
  aiTemperature: number
}

export type AuthResponse = {
  accessToken: string
  expiresIn: number
  username: string
  kdfSalt: string
  userId: string
}

export type AboutInfo = {
  name: string
  version: string
  description: string
  author: string
}

export const ITEM_TYPES: { id: ItemType; label: string; secretLabel: string }[] = [
  { id: 'login', label: '登录账号', secretLabel: '密码' },
  { id: 'credential', label: '凭据密码', secretLabel: '凭据 / 密码' },
  { id: 'key', label: '密钥 / Key', secretLabel: '密钥内容' },
  { id: 'note', label: '备忘', secretLabel: '内容' },
]
