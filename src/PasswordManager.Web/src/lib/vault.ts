import type { CustomField, Entry } from '../types'
import { decryptText, encryptText } from './crypto'

export async function decryptEntry(key: CryptoKey, entry: Entry): Promise<Entry> {
  const customFields: CustomField[] = []
  for (const field of entry.customFields ?? []) {
    customFields.push({
      ...field,
      value: field.isHidden ? await decryptText(key, field.value) : field.value,
    })
  }
  return {
    ...entry,
    password: await decryptText(key, entry.password),
    notes: await decryptText(key, entry.notes),
    customFields,
  }
}

export async function encryptEntryPayload(
  key: CryptoKey,
  data: {
    title: string
    username: string
    password: string
    url: string
    notes: string
    category: string
    groupId: string | null
    customFields: CustomField[]
  },
) {
  const customFields: CustomField[] = []
  for (const field of data.customFields) {
    customFields.push({
      key: field.key,
      isHidden: field.isHidden,
      value: field.isHidden ? await encryptText(key, field.value) : field.value,
    })
  }
  return {
    title: data.title,
    username: data.username,
    password: await encryptText(key, data.password),
    url: data.url,
    notes: await encryptText(key, data.notes),
    category: data.category,
    groupId: data.groupId,
    customFields,
  }
}
