export function generatePassword(options: {
  length: number
  upper: boolean
  lower: boolean
  digits: boolean
  symbols: boolean
}): string {
  const { length, upper, lower, digits, symbols } = options
  let charset = ''
  if (upper) charset += 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
  if (lower) charset += 'abcdefghijklmnopqrstuvwxyz'
  if (digits) charset += '0123456789'
  if (symbols) charset += '!@#$%^&*()_+-=[]{}|;:,.<>?'
  if (!charset) throw new Error('请至少选择一种字符类型')

  const arr = new Uint32Array(length)
  crypto.getRandomValues(arr)
  let pwd = ''
  for (let i = 0; i < length; i++) pwd += charset[arr[i]! % charset.length]
  return pwd
}

export function checkStrength(password: string): { label: string; color: string; width: string } {
  if (!password) return { label: '--', color: '#bbb', width: '0%' }
  let score = 0
  if (password.length >= 8) score++
  if (password.length >= 12) score++
  if (password.length >= 16) score++
  if (/[A-Z]/.test(password)) score++
  if (/[a-z]/.test(password)) score++
  if (/[0-9]/.test(password)) score++
  if (/[^A-Za-z0-9]/.test(password)) score++

  if (score <= 2) return { label: '弱', color: '#e53935', width: '25%' }
  if (score <= 4) return { label: '中等', color: '#ff9800', width: '50%' }
  if (score <= 6) return { label: '强', color: '#4caf50', width: '75%' }
  return { label: '非常强', color: '#1b5e20', width: '100%' }
}
