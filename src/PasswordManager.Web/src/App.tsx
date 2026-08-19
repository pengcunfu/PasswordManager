import { SessionProvider, useSession } from './context/SessionContext'
import { LoginPage } from './pages/LoginPage'
import { VaultPage } from './pages/VaultPage'

export default function App() {
  return (
    <SessionProvider>
      <Gate />
    </SessionProvider>
  )
}

function Gate() {
  const { session, ready } = useSession()
  if (!ready) {
    return (
      <div className="login-page">
        <div className="login-box">
          <div className="icon">🔒</div>
          <h1>密码管家</h1>
          <div className="subtitle">正在恢复会话...</div>
        </div>
      </div>
    )
  }
  return session ? <VaultPage /> : <LoginPage />
}
