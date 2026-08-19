export type CustomField = {
  key: string
  value: string
  isHidden: boolean
}

export type Entry = {
  id: string
  title: string
  username: string
  password: string
  url: string
  notes: string
  category: string
  groupId: string | null
  customFields: CustomField[]
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
