import type { Account, CustomField, Group, VaultDoc, VaultItem } from '../types'
import { decryptText, encryptText } from './crypto'

export function emptyVault(): VaultDoc {
  return { version: '4.0', groups: [], items: [] }
}

export function newId() {
  return crypto.randomUUID()
}

export function nowIso() {
  return new Date().toISOString()
}

export function emptyAccount(): Account {
  return {
    id: newId(),
    label: '默认',
    username: '',
    secret: '',
    notes: '',
    fields: [],
  }
}

export function emptyItem(): VaultItem {
  const t = nowIso()
  return {
    id: newId(),
    type: 'login',
    title: '',
    url: '',
    groupId: null,
    category: '',
    notes: '',
    accounts: [emptyAccount()],
    createdAt: t,
    updatedAt: t,
  }
}

export function emptyGroup(name = ''): Group {
  const t = nowIso()
  return {
    id: newId(),
    name,
    description: '',
    color: '#18A058',
    sortOrder: 0,
    createdAt: t,
    updatedAt: t,
  }
}

export function parseVault(raw: unknown): VaultDoc {
  const doc = (raw ?? {}) as Partial<VaultDoc>
  return {
    version: doc.version || '4.0',
    groups: Array.isArray(doc.groups) ? doc.groups : [],
    items: Array.isArray(doc.items) ? doc.items.map(normalizeItem) : [],
  }
}

export function cloneVault(vault: VaultDoc): VaultDoc {
  return structuredClone(vault)
}

export function normalizeUrl(url: string) {
  return (url || '').trim().toLowerCase()
}

export function itemMergeKey(url: string, title: string) {
  const u = normalizeUrl(url)
  return u ? `url:${u}` : `title:${(title || '').trim().toLowerCase()}`
}

export function findItemByKey(vault: VaultDoc, url: string, title: string, exceptId?: string) {
  const key = itemMergeKey(url, title)
  return vault.items.find((item) => item.id !== exceptId && itemMergeKey(item.url, item.title) === key)
}

function normalizeItem(item: VaultItem): VaultItem {
  const raw = item as VaultItem & { password?: string; customFields?: Account['fields'] }
  const accounts = Array.isArray(item.accounts) ? item.accounts : []
    const mapped = accounts.map((acc) => {
    const a = acc as Account & { password?: string; customFields?: Account['fields'] }
    const rawFields = a.fields ?? a.customFields ?? []
    return {
      ...acc,
      secret: a.secret || a.password || '',
      notes: a.notes || '',
      fields: rawFields.map((f) => ({
        key: f.key || '',
        value: f.value || '',
        isHidden: Boolean(f.isHidden || (f as { is_hidden?: boolean }).is_hidden),
      })),
    }
  })
  return {
    ...item,
    type: item.type || 'login',
    url: item.url || '',
    groupId: item.groupId ?? null,
    category: item.category || '',
    notes: item.notes || '',
    accounts: mapped.length > 0
      ? mapped
      : [{
          ...emptyAccount(),
          username: '',
          secret: raw.password || '',
          fields: raw.customFields ?? [],
        }],
  }
}

export async function decryptVault(key: CryptoKey, vault: VaultDoc): Promise<VaultDoc> {
  const items: VaultItem[] = []
  for (const item of vault.items) {
    const accounts: Account[] = []
    for (const acc of item.accounts) {
      accounts.push({
        ...acc,
        secret: await decryptText(key, acc.secret),
        notes: await decryptText(key, acc.notes),
        fields: await decryptFields(key, acc.fields ?? []),
      })
    }
    items.push({
      ...item,
      notes: await decryptText(key, item.notes),
      accounts,
    })
  }
  return { ...vault, items }
}

export async function encryptVault(key: CryptoKey, vault: VaultDoc): Promise<VaultDoc> {
  const items: VaultItem[] = []
  for (const item of vault.items) {
    const accounts: Account[] = []
    for (const acc of item.accounts) {
      accounts.push({
        ...acc,
        secret: await encryptText(key, acc.secret),
        notes: await encryptText(key, acc.notes),
        fields: await encryptFields(key, acc.fields ?? []),
      })
    }
    items.push({
      ...item,
      notes: await encryptText(key, item.notes),
      accounts,
    })
  }
  return { ...vault, items }
}

async function decryptFields(key: CryptoKey, fields: CustomField[]): Promise<CustomField[]> {
  const result: CustomField[] = []
  for (const field of fields) {
    result.push({
      ...field,
      value: field.isHidden ? await decryptText(key, field.value) : field.value,
    })
  }
  return result
}

async function encryptFields(key: CryptoKey, fields: CustomField[]): Promise<CustomField[]> {
  const result: CustomField[] = []
  for (const field of fields) {
    result.push({
      ...field,
      value: field.isHidden ? await encryptText(key, field.value) : field.value,
    })
  }
  return result
}
