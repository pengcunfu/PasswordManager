const ITERATIONS = 100_000
const KEY_LENGTH = 256
const IV_SIZE = 12
const SALT_SIZE = 32

const textEncoder = new TextEncoder()
const textDecoder = new TextDecoder()

export function generateSalt(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(SALT_SIZE))
  return bytesToBase64(bytes)
}

export async function deriveKey(password: string, saltB64: string): Promise<CryptoKey> {
  const salt = base64ToBytes(saltB64)
  const material = await crypto.subtle.importKey(
    'raw',
    textEncoder.encode(password),
    'PBKDF2',
    false,
    ['deriveKey'],
  )
  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', salt: salt.buffer, iterations: ITERATIONS, hash: 'SHA-256' },
    material,
    { name: 'AES-GCM', length: KEY_LENGTH },
    true,
    ['encrypt', 'decrypt'],
  )
}

export async function exportKeyRaw(key: CryptoKey): Promise<string> {
  const raw = await crypto.subtle.exportKey('raw', key)
  return bytesToBase64(new Uint8Array(raw))
}

export async function importKeyRaw(b64: string): Promise<CryptoKey> {
  const bytes = base64ToBytes(b64)
  return crypto.subtle.importKey(
    'raw',
    bytes.buffer,
    { name: 'AES-GCM', length: KEY_LENGTH },
    true,
    ['encrypt', 'decrypt'],
  )
}

export async function encryptText(key: CryptoKey, plaintext: string): Promise<string> {
  if (!plaintext) return ''
  const iv = crypto.getRandomValues(new Uint8Array(IV_SIZE))
  const cipher = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv },
    key,
    textEncoder.encode(plaintext),
  )
  const packed = new Uint8Array(iv.length + cipher.byteLength)
  packed.set(iv, 0)
  packed.set(new Uint8Array(cipher), iv.length)
  return bytesToBase64(packed)
}

export async function deriveCbcKey(password: string, saltB64: string): Promise<CryptoKey> {
  const salt = base64ToBytes(saltB64)
  const material = await crypto.subtle.importKey(
    'raw',
    textEncoder.encode(password),
    'PBKDF2',
    false,
    ['deriveKey'],
  )
  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', salt: salt.buffer, iterations: ITERATIONS, hash: 'SHA-256' },
    material,
    { name: 'AES-CBC', length: KEY_LENGTH },
    false,
    ['decrypt'],
  )
}

export async function decryptAesCbc(key: CryptoKey, ciphertext: string): Promise<string> {
  if (!ciphertext) return ''
  const packed = base64ToBytes(ciphertext)
  if (packed.length <= 16) throw new Error('密文无效')
  const iv = packed.slice(0, 16)
  const data = packed.slice(16)
  const plain = await crypto.subtle.decrypt({ name: 'AES-CBC', iv: iv.buffer }, key, data.buffer)
  return textDecoder.decode(plain)
}

export async function decryptText(key: CryptoKey, ciphertext: string): Promise<string> {
  if (!ciphertext) return ''
  const packed = base64ToBytes(ciphertext)
  const iv = packed.slice(0, IV_SIZE)
  const data = packed.slice(IV_SIZE)
  const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: iv.buffer }, key, data.buffer)
  return textDecoder.decode(plain)
}

function bytesToBase64(bytes: Uint8Array): string {
  let binary = ''
  bytes.forEach((b) => {
    binary += String.fromCharCode(b)
  })
  return btoa(binary)
}

function base64ToBytes(b64: string): Uint8Array<ArrayBuffer> {
  const binary = atob(b64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i)
  return bytes
}
