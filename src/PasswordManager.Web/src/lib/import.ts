import type { CustomField } from '../types'
import { decryptAesCbc, decryptText, deriveCbcKey, deriveKey } from './crypto'
import { encryptEntryPayload } from './vault'

export type ImportPlainEntry = {
  title: string
  username: string
  password: string
  url: string
  notes: string
  category: string
  groupName: string | null
  customFields: CustomField[]
}

export type ImportPlainGroup = {
  name: string
  description: string
  color: string
  sortOrder: number
}

export type DetectedImport = {
  format: string
  groups: ImportPlainGroup[]
  entries: ImportPlainEntry[]
  needsPassword: boolean
  legacySalt?: string
  kdfSalt?: string
  encrypted?: boolean
}

export type ImportPayload = {
  skipDuplicates: boolean
  groups: ImportPlainGroup[]
  entries: Array<{
    title: string
    username: string
    password: string
    url: string
    notes: string
    category: string
    groupName: string | null
    customFields: CustomField[]
  }>
}

export function detectImport(text: string): DetectedImport {
  const trimmed = text.trim()
  if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
    const json = JSON.parse(trimmed) as Record<string, unknown>
    if (json.items && Array.isArray(json.items)) return parseBitwarden(json)
    if (json.entries && json.salt && !json.exportedAt) return parseLegacy(json)
    if (json.entries) return parseBackup(json)
    throw new Error('无法识别该 JSON 文件格式')
  }
  return parseCsv(trimmed)
}

export async function decryptDetected(
  detected: DetectedImport,
  currentKey: CryptoKey,
  originalPassword?: string,
): Promise<ImportPlainEntry[]> {
  if (!detected.encrypted) return detected.entries
  if (detected.entries.length === 0) return []

  let key = currentKey
  const sample = detected.entries.find((e) => e.password)
  const pwd = originalPassword?.trim()

  if (detected.needsPassword) {
    if (!pwd) throw new Error('请输入原主密码以解密文件')
    if (detected.legacySalt) key = await deriveCbcKey(pwd, detected.legacySalt)
    else if (detected.kdfSalt) key = await deriveKey(pwd, detected.kdfSalt)
    else throw new Error('文件缺少盐值，无法解密')
  } else if (sample) {
    try {
      await decryptText(currentKey, sample.password)
    } catch {
      if (!detected.kdfSalt) throw new Error('无法解密该备份，请确认是用当前账号导出的文件')
      if (!pwd) throw new Error('该备份使用其他主密码加密，请输入原主密码')
      key = await deriveKey(pwd, detected.kdfSalt)
      await decryptText(key, sample.password)
    }
  }

  try {
    const result: ImportPlainEntry[] = []
    for (const entry of detected.entries) {
      result.push(await decryptImportedEntry(key, entry, !!detected.legacySalt))
    }
    return result
  } catch {
    throw new Error('解密失败，请检查原主密码是否正确')
  }
}

export async function buildImportPayload(
  groups: ImportPlainGroup[],
  entries: ImportPlainEntry[],
  currentKey: CryptoKey,
  skipDuplicates: boolean,
): Promise<ImportPayload> {
  const encrypted = []
  for (const entry of entries) {
    if (!entry.title.trim()) continue
    const payload = await encryptEntryPayload(currentKey, {
      title: entry.title.trim(),
      username: entry.username,
      password: entry.password,
      url: entry.url,
      notes: entry.notes,
      category: entry.category,
      groupId: null,
      customFields: entry.customFields,
    })
    encrypted.push({
      ...payload,
      groupName: entry.groupName,
    })
  }
  return { skipDuplicates, groups, entries: encrypted }
}

async function decryptImportedEntry(
  key: CryptoKey,
  entry: ImportPlainEntry,
  cbc: boolean,
): Promise<ImportPlainEntry> {
  const dec = cbc ? decryptAesCbc : decryptText
  const customFields: CustomField[] = []
  for (const field of entry.customFields) {
    customFields.push({
      ...field,
      value: field.isHidden && field.value ? await dec(key, field.value) : field.value,
    })
  }
  return {
    ...entry,
    password: entry.password ? await dec(key, entry.password) : '',
    notes: entry.notes ? await dec(key, entry.notes) : '',
    customFields,
  }
}

function parseBackup(json: Record<string, unknown>): DetectedImport {
  const groups = parseGroups(json.groups)
  const rawGroups = Array.isArray(json.groups) ? json.groups as Array<Record<string, unknown>> : []
  const idToName = new Map<string, string>()
  for (const g of rawGroups) {
    const id = String(g.id ?? '')
    const name = String(g.name ?? '')
    if (id && name) idToName.set(id, name)
  }

  const entries = asArray(json.entries).map((raw) => {
    const groupId = raw.groupId == null ? '' : String(raw.groupId)
    return {
      title: String(raw.title ?? ''),
      username: String(raw.username ?? ''),
      password: String(raw.password ?? ''),
      url: String(raw.url ?? ''),
      notes: String(raw.notes ?? ''),
      category: String(raw.category ?? ''),
      groupName: (groupId && idToName.get(groupId)) || null,
      customFields: parseCustomFields(raw.customFields),
    }
  }).filter((e) => e.title)

  const kdfSalt = json.kdfSalt ? String(json.kdfSalt) : undefined
  return {
    format: '凭据管理器备份',
    groups,
    entries,
    needsPassword: false,
    kdfSalt,
    encrypted: true,
  }
}

function parseLegacy(json: Record<string, unknown>): DetectedImport {
  const salt = String(json.salt ?? '')
  if (!salt) throw new Error('旧版密码库缺少盐值')
  const groups = parseGroups(json.groups)
  const idToName = new Map<string, string>()
  const rawGroups = asArray(json.groups)
  for (const g of rawGroups) {
    const id = String(g.id ?? '')
    const name = String(g.name ?? '')
    if (id && name) idToName.set(id, name)
  }

  const entries = asArray(json.entries).map((raw) => {
    const groupId = String(raw.group_id ?? raw.groupId ?? '')
    return {
      title: String(raw.title ?? ''),
      username: String(raw.username ?? ''),
      password: String(raw.password ?? ''),
      url: String(raw.url ?? ''),
      notes: String(raw.notes ?? ''),
      category: String(raw.category ?? ''),
      groupName: (groupId && idToName.get(groupId)) || null,
      customFields: parseCustomFields(raw.custom_fields ?? raw.customFields),
    }
  }).filter((e) => e.title)

  return {
    format: '旧版本地密码库',
    groups,
    entries,
    needsPassword: true,
    legacySalt: salt,
    encrypted: true,
  }
}

function parseBitwarden(json: Record<string, unknown>): DetectedImport {
  const folders = asArray(json.folders)
  const folderNames = new Map<string, string>()
  for (const f of folders) {
    folderNames.set(String(f.id ?? ''), String(f.name ?? ''))
  }
  const groups = folders
    .map((f, i) => ({
      name: String(f.name ?? ''),
      description: '',
      color: '#4A90E2',
      sortOrder: i,
    }))
    .filter((g) => g.name)

  const entries: ImportPlainEntry[] = []
  for (const item of asArray(json.items)) {
    const login = (item.login ?? {}) as Record<string, unknown>
    const uris = Array.isArray(login.uris) ? login.uris as Array<Record<string, unknown>> : []
    const folderId = item.folderId == null ? '' : String(item.folderId)
    entries.push({
      title: String(item.name ?? '未命名'),
      username: String(login.username ?? ''),
      password: String(login.password ?? ''),
      url: String(uris[0]?.uri ?? ''),
      notes: String(item.notes ?? ''),
      category: '',
      groupName: (folderId && folderNames.get(folderId)) || null,
      customFields: [],
    })
  }

  return {
    format: 'Bitwarden JSON',
    groups,
    entries: entries.filter((e) => e.title),
    needsPassword: false,
    encrypted: false,
  }
}

function parseCsv(text: string): DetectedImport {
  const rows = parseCsvRows(text)
  if (rows.length < 2) throw new Error('CSV 文件没有数据')
  const header = rows[0]!.map((h) => h.trim().toLowerCase())
  const idx = (names: string[]) => names.map((n) => header.indexOf(n)).find((i) => i >= 0) ?? -1

  const titleI = idx(['name', 'title', '名称', '标题'])
  const urlI = idx(['url', 'login_uri', 'uri', '网址'])
  const userI = idx(['username', 'login_username', 'user', '用户名'])
  const passI = idx(['password', 'login_password', '密码'])
  const notesI = idx(['notes', 'note', '备注'])
  const folderI = idx(['folder', 'group', 'grouping', '分组'])

  if (titleI < 0 && urlI < 0) throw new Error('无法识别 CSV 列，需要包含 name/title 或 url')

  const groupSet = new Set<string>()
  const entries: ImportPlainEntry[] = []
  for (const row of rows.slice(1)) {
    const title = cell(row, titleI) || hostFromUrl(cell(row, urlI)) || '未命名'
    const groupName = cell(row, folderI) || null
    if (groupName) groupSet.add(groupName)
    entries.push({
      title,
      username: cell(row, userI),
      password: cell(row, passI),
      url: cell(row, urlI),
      notes: cell(row, notesI),
      category: '',
      groupName,
      customFields: [],
    })
  }

  return {
    format: 'CSV',
    groups: [...groupSet].map((name, i) => ({ name, description: '', color: '#4A90E2', sortOrder: i })),
    entries,
    needsPassword: false,
    encrypted: false,
  }
}

function parseGroups(raw: unknown): ImportPlainGroup[] {
  return asArray(raw).map((g, i) => ({
    name: String(g.name ?? ''),
    description: String(g.description ?? ''),
    color: String(g.color ?? '#4A90E2'),
    sortOrder: Number(g.sortOrder ?? g.sort_order ?? i) || i,
  })).filter((g) => g.name)
}

function parseCustomFields(raw: unknown): CustomField[] {
  return asArray(raw).map((f) => ({
    key: String(f.key ?? ''),
    value: String(f.value ?? ''),
    isHidden: Boolean(f.isHidden ?? f.is_hidden),
  })).filter((f) => f.key)
}

function asArray(raw: unknown): Array<Record<string, unknown>> {
  return Array.isArray(raw) ? raw as Array<Record<string, unknown>> : []
}

function cell(row: string[], index: number) {
  if (index < 0) return ''
  return (row[index] ?? '').trim()
}

function hostFromUrl(url: string) {
  try {
    return new URL(url).hostname.replace(/^www\./, '')
  } catch {
    return url
  }
}

function parseCsvRows(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let cur = ''
  let quoted = false
  for (let i = 0; i < text.length; i++) {
    const ch = text[i]!
    if (quoted) {
      if (ch === '"') {
        if (text[i + 1] === '"') {
          cur += '"'
          i++
        } else quoted = false
      } else cur += ch
    } else if (ch === '"') quoted = true
    else if (ch === ',') {
      row.push(cur)
      cur = ''
    } else if (ch === '\n') {
      row.push(cur.replace(/\r$/, ''))
      rows.push(row)
      row = []
      cur = ''
    } else cur += ch
  }
  if (cur.length > 0 || row.length > 0) {
    row.push(cur.replace(/\r$/, ''))
    rows.push(row)
  }
  return rows.filter((r) => r.some((c) => c.trim()))
}
