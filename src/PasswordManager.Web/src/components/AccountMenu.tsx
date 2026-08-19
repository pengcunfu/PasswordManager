import { useEffect, useRef, useState } from 'react'
import { useSession } from '../context/SessionContext'

export function AccountMenu() {
  const {
    session,
    accounts,
    switchAccount,
    startAddAccount,
    logout,
    logoutAll,
  } = useSession()
  const [open, setOpen] = useState(false)
  const [error, setError] = useState('')
  const box = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function onDoc(e: PointerEvent) {
      if (!box.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('pointerdown', onDoc)
    return () => document.removeEventListener('pointerdown', onDoc)
  }, [])

  async function onSwitch(userId: string) {
    if (userId === session?.userId) {
      setOpen(false)
      return
    }
    setError('')
    try {
      await switchAccount(userId)
      setOpen(false)
    } catch (e) {
      setError(e instanceof Error ? e.message : '切换失败')
    }
  }

  return (
    <div className="account-menu-wrap" ref={box}>
      <button type="button" className="account-chip" onClick={() => { setOpen((v) => !v); setError('') }}>
        <span className="account-chip-name">{session?.username ?? '账号'}</span>
        <span className="account-chip-caret">▾</span>
      </button>
      {open && (
        <div className="account-menu">
          {accounts.map((a) => (
            <button
              key={a.userId}
              type="button"
              className={`account-menu-item ${a.userId === session?.userId ? 'active' : ''}`}
              onClick={() => void onSwitch(a.userId)}
            >
              <span>{a.username}</span>
              {a.userId === session?.userId && <span className="account-menu-tag">当前</span>}
            </button>
          ))}
          {error && <div className="account-menu-error">{error}</div>}
          <div className="account-menu-sep" />
          <button type="button" className="account-menu-item" onClick={() => { setOpen(false); startAddAccount() }}>
            添加账号
          </button>
          <button type="button" className="account-menu-item" onClick={() => { setOpen(false); void logout() }}>
            锁定当前账号
          </button>
          {accounts.length > 1 && (
            <button type="button" className="account-menu-item danger" onClick={() => { setOpen(false); void logoutAll() }}>
              退出全部账号
            </button>
          )}
        </div>
      )}
    </div>
  )
}
