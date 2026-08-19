import { useState, type FormEvent } from 'react'
import { useSession } from '../context/SessionContext'

export function LoginPage() {
  const { login, register } = useSession()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [username, setUsername] = useState(localStorage.getItem('pm.username') || '')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!username.trim() || !password) {
      setError('请输入用户名和主密码')
      return
    }
    if (mode === 'register') {
      if (password.length < 8) {
        setError('主密码至少 8 位')
        return
      }
      if (password !== confirm) {
        setError('两次输入的密码不一致')
        return
      }
    }
    setBusy(true)
    try {
      if (mode === 'login') await login(username, password)
      else await register(username, password)
      localStorage.setItem('pm.username', username.trim())
    } catch (err) {
      setError(err instanceof Error ? err.message : '操作失败')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login-page">
      <form className="login-box" onSubmit={onSubmit}>
        <div className="icon">🔒</div>
        <h1>凭据管理器</h1>
        <div className="subtitle">
          {mode === 'login' ? '输入主密码以解锁您的密码库' : '创建账号，主密码用于派生加密密钥'}
        </div>
        <div className="error">{error}</div>
        <input
          autoComplete="username"
          placeholder="用户名"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <input
          type="password"
          autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
          placeholder="主密码"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        {mode === 'register' && (
          <input
            type="password"
            autoComplete="new-password"
            placeholder="确认主密码"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
          />
        )}
        <button className="primary" disabled={busy}>
          {busy ? '处理中...' : mode === 'login' ? '解 锁' : '注 册'}
        </button>
        <div className="switch">
          {mode === 'login' ? (
            <>
              还没有账号？
              <button type="button" onClick={() => { setMode('register'); setError('') }}>
                注册
              </button>
            </>
          ) : (
            <>
              已有账号？
              <button type="button" onClick={() => { setMode('login'); setError('') }}>
                登录
              </button>
            </>
          )}
        </div>
      </form>
    </div>
  )
}
