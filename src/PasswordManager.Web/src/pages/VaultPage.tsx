import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../lib/api'
import { runAiChat, type ChatMessage } from '../lib/ai'
import { checkStrength, generatePassword } from '../lib/password'
import { decryptEntry, encryptEntryPayload } from '../lib/vault'
import { useSession } from '../context/SessionContext'
import type { AboutInfo, CustomField, Entry, Group, Settings } from '../types'

type Toast = { msg: string; type: 'success' | 'error' }
type MobilePane = 'list' | 'detail' | 'ai'

const emptyForm = {
  title: '',
  username: '',
  password: '',
  url: '',
  notes: '',
  category: '',
  groupId: null as string | null,
  customFields: [] as CustomField[],
}

export function VaultPage() {
  const { session, logout } = useSession()
  const key = session!.key

  const [entries, setEntries] = useState<Entry[]>([])
  const [groups, setGroups] = useState<Group[]>([])
  const [settings, setSettings] = useState<Settings | null>(null)
  const [keyword, setKeyword] = useState('')
  const [groupId, setGroupId] = useState<string | 'all'>('all')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<Entry | null>(null)
  const [editing, setEditing] = useState<typeof emptyForm | null>(null)
  const [toast, setToast] = useState<Toast | null>(null)
  const [pane, setPane] = useState<MobilePane>('list')
  const [showGroups, setShowGroups] = useState(false)
  const [showGen, setShowGen] = useState(false)
  const [genTarget, setGenTarget] = useState<'modal' | 'form'>('modal')
  const [about, setAbout] = useState<AboutInfo | null>(null)
  const [aiSettingsOpen, setAiSettingsOpen] = useState(false)
  const [groupEditor, setGroupEditor] = useState<Partial<Group> | null>(null)

  const showToast = (msg: string, type: Toast['type'] = 'success') => {
    setToast({ msg, type })
    setTimeout(() => setToast(null), 2500)
  }

  const reload = useCallback(async () => {
    const [rawEntries, rawGroups] = await Promise.all([api.listEntries(), api.listGroups()])
    const decrypted: Entry[] = []
    for (const e of rawEntries) decrypted.push(await decryptEntry(key, e))
    setEntries(decrypted)
    setGroups(rawGroups)
  }, [key])

  useEffect(() => {
    reload().catch((e) => showToast(e.message, 'error'))
    api.getSettings().then(setSettings).catch(() => undefined)
  }, [reload])

  const filtered = useMemo(() => {
    const q = keyword.trim().toLowerCase()
    return entries.filter((e) => {
      if (groupId !== 'all' && (e.groupId || '') !== groupId) return false
      if (!q) return true
      return [e.title, e.username, e.url, e.category, e.notes].some((v) =>
        (v || '').toLowerCase().includes(q),
      )
    })
  }, [entries, keyword, groupId])

  async function openEntry(id: string) {
    setSelectedId(id)
    setEditing(null)
    const raw = await api.getEntry(id)
    const dec = await decryptEntry(key, raw)
    setDetail(dec)
    setPane('detail')
  }

  function startAdd() {
    setSelectedId(null)
    setDetail(null)
    setEditing({
      ...emptyForm,
      groupId: groupId === 'all' ? null : groupId,
    })
    setPane('detail')
  }

  function startEdit() {
    if (!detail) return
    setEditing({
      title: detail.title,
      username: detail.username,
      password: detail.password,
      url: detail.url,
      notes: detail.notes,
      category: detail.category,
      groupId: detail.groupId,
      customFields: detail.customFields,
    })
  }

  async function saveEntry() {
    if (!editing?.title.trim()) {
      showToast('请输入标题', 'error')
      return
    }
    const payload = await encryptEntryPayload(key, {
      ...editing,
      title: editing.title.trim(),
    })
    try {
      if (selectedId) {
        const saved = await decryptEntry(key, await api.updateEntry(selectedId, payload))
        setDetail(saved)
        showToast('更新成功')
      } else {
        const saved = await decryptEntry(key, await api.createEntry(payload))
        setSelectedId(saved.id)
        setDetail(saved)
        showToast('添加成功')
      }
      setEditing(null)
      await reload()
    } catch (e) {
      showToast(e instanceof Error ? e.message : '保存失败', 'error')
    }
  }

  async function removeEntry() {
    if (!selectedId || !confirm('确定要删除这个密码条目吗？')) return
    await api.deleteEntry(selectedId)
    setSelectedId(null)
    setDetail(null)
    setEditing(null)
    setPane('list')
    showToast('已删除')
    await reload()
  }

  async function copyText(text: string) {
    await navigator.clipboard.writeText(text)
    showToast('已复制到剪贴板')
  }

  async function doBackup() {
    const data = await api.backup()
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `password-backup-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
    showToast('备份已下载')
  }

  async function saveGroup() {
    if (!groupEditor?.name?.trim()) return
    const body = {
      name: groupEditor.name.trim(),
      description: groupEditor.description || '',
      color: groupEditor.color || '#4A90E2',
      sortOrder: groupEditor.sortOrder || 0,
    }
    if (groupEditor.id) await api.updateGroup(groupEditor.id, body)
    else await api.createGroup(body)
    setGroupEditor(null)
    await reload()
  }

  const groupsUi = (
    <>
      <div className="panel-title">
        分组
        <button className="icon-btn" onClick={() => setGroupEditor({ name: '', color: '#18A058', sortOrder: groups.length })}>+</button>
      </div>
      <div className="scroll">
        <div className={`group-item ${groupId === 'all' ? 'active' : ''}`} onClick={() => { setGroupId('all'); setShowGroups(false) }}>
          全部
        </div>
        <div className={`group-item ${groupId === '' ? 'active' : ''}`} onClick={() => { setGroupId(''); setShowGroups(false) }}>
          未分组
        </div>
        {groups.map((g) => (
          <div key={g.id} className={`group-item ${groupId === g.id ? 'active' : ''}`} onClick={() => { setGroupId(g.id); setShowGroups(false) }}>
            <span className="group-dot" style={{ background: g.color }} />
            {g.name}
            <span style={{ float: 'right', color: '#bbb' }} onClick={(e) => { e.stopPropagation(); setGroupEditor(g) }}>✎</span>
          </div>
        ))}
      </div>
    </>
  )

  return (
    <div className="app-shell">
      <div className="toolbar">
        <button className="menu-btn icon-btn" onClick={() => setShowGroups(true)}>☰</button>
        <div className="toolbar-title"><span>密码管家</span></div>
        <div className="toolbar-right">
          <button className="hide-sm" onClick={() => { setGenTarget('modal'); setShowGen(true) }}>密码生成器</button>
          <button className="hide-sm" onClick={() => api.about().then(setAbout)}>关于</button>
          <button className="hide-sm" onClick={() => doBackup().catch((e) => showToast(e.message, 'error'))}>备份</button>
          <button onClick={() => logout()}>注销</button>
        </div>
      </div>

      <div className="columns" style={{ position: 'relative' }}>
        <div className="col col-groups desktop-only">{groupsUi}</div>

        <div className="col col-list" style={{ display: pane === 'ai' ? undefined : undefined }}>
          <div className="search-box">
            <input placeholder="搜索密码..." value={keyword} onChange={(e) => setKeyword(e.target.value)} />
          </div>
          <div className="list-count">{filtered.length} 个条目</div>
          <div className="scroll">
            {filtered.map((e) => (
              <div key={e.id} className={`entry-item ${e.id === selectedId ? 'active' : ''}`} onClick={() => openEntry(e.id).catch((err) => showToast(err.message, 'error'))}>
                <div className="entry-title">{e.title}</div>
                <div className="entry-meta">{e.username}</div>
                {e.category && <span className="entry-category">{e.category}</span>}
              </div>
            ))}
          </div>
          <button className="add-btn" onClick={startAdd}>+ 添加密码</button>
        </div>

        <div className={`col col-detail ${pane === 'detail' ? 'mobile-show' : ''}`}>
          {editing ? (
            <div className="edit-form">
              <h2 style={{ fontSize: 17, marginBottom: 20 }}>{selectedId ? '编辑密码' : '添加密码'}</h2>
              <Field label="标题 *"><input value={editing.title} onChange={(e) => setEditing({ ...editing, title: e.target.value })} /></Field>
              <Field label="用户名"><input value={editing.username} onChange={(e) => setEditing({ ...editing, username: e.target.value })} /></Field>
              <Field label="密码">
                <div style={{ display: 'flex', gap: 6 }}>
                  <input style={{ flex: 1 }} value={editing.password} onChange={(e) => setEditing({ ...editing, password: e.target.value })} />
                  <button className="btn-secondary" type="button" onClick={() => { setGenTarget('form'); setShowGen(true) }}>生成</button>
                </div>
              </Field>
              <Field label="网址"><input value={editing.url} onChange={(e) => setEditing({ ...editing, url: e.target.value })} /></Field>
              <Field label="分类"><input value={editing.category} onChange={(e) => setEditing({ ...editing, category: e.target.value })} placeholder="如：邮箱、社交、开发工具" /></Field>
              <Field label="分组">
                <select value={editing.groupId || ''} onChange={(e) => setEditing({ ...editing, groupId: e.target.value || null })}>
                  <option value="">未分组</option>
                  {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
                </select>
              </Field>
              <Field label="备注"><textarea value={editing.notes} onChange={(e) => setEditing({ ...editing, notes: e.target.value })} /></Field>
              <div className="form-row">
                <label>自定义字段</label>
                {editing.customFields.map((f, i) => (
                  <div key={i} style={{ display: 'flex', gap: 6, marginBottom: 6 }}>
                    <input placeholder="名称" value={f.key} onChange={(e) => {
                      const next = [...editing.customFields]; next[i] = { ...f, key: e.target.value }; setEditing({ ...editing, customFields: next })
                    }} />
                    <input placeholder="值" value={f.value} onChange={(e) => {
                      const next = [...editing.customFields]; next[i] = { ...f, value: e.target.value }; setEditing({ ...editing, customFields: next })
                    }} />
                    <label style={{ whiteSpace: 'nowrap' }}>
                      <input type="checkbox" checked={f.isHidden} onChange={(e) => {
                        const next = [...editing.customFields]; next[i] = { ...f, isHidden: e.target.checked }; setEditing({ ...editing, customFields: next })
                      }} /> 隐藏
                    </label>
                    <button className="btn-secondary" type="button" onClick={() => setEditing({ ...editing, customFields: editing.customFields.filter((_, j) => j !== i) })}>删</button>
                  </div>
                ))}
                <button className="btn-secondary" type="button" onClick={() => setEditing({ ...editing, customFields: [...editing.customFields, { key: '', value: '', isHidden: false }] })}>+ 字段</button>
              </div>
              <div className="form-actions">
                <button className="btn-primary" onClick={saveEntry}>保存</button>
                <button className="btn-secondary" onClick={() => { setEditing(null); if (!selectedId) setPane('list') }}>取消</button>
              </div>
            </div>
          ) : detail ? (
            <div className="detail-view">
              <div className="detail-header">
                <button className="mobile-only icon-btn" onClick={() => setPane('list')}>←</button>
                <span className="title">{detail.title}</span>
                {detail.category && <span className="entry-category">{detail.category}</span>}
                <div className="detail-actions">
                  <button className="icon-btn" onClick={startEdit}>编辑</button>
                  <button className="icon-btn btn-danger" onClick={removeEntry}>删除</button>
                </div>
              </div>
              <FieldView label="用户名" value={detail.username} onCopy={copyText} />
              <div className="field-group">
                <div className="field-label">密码</div>
                <div className="field-value pwd-field">
                  <span>••••••••</span>
                  <button className="copy-btn" onClick={() => copyText(detail.password)}>复制</button>
                </div>
              </div>
              {detail.url && (
                <div className="field-group">
                  <div className="field-label">网址</div>
                  <div className="field-value"><a href={detail.url} target="_blank" rel="noreferrer">{detail.url}</a></div>
                </div>
              )}
              {detail.notes && <FieldView label="备注" value={detail.notes} />}
              {detail.customFields.map((f, i) => (
                <FieldView key={i} label={f.key} value={f.isHidden ? '••••••••' : f.value} onCopy={f.isHidden ? () => copyText(f.value) : undefined} />
              ))}
              <div style={{ marginTop: 20, fontSize: 11, color: '#bbb' }}>
                创建：{formatTime(detail.createdAt)}　|　更新：{formatTime(detail.updatedAt)}
              </div>
            </div>
          ) : (
            <div className="detail-empty">
              <div>选择一个密码条目查看详情</div>
            </div>
          )}
        </div>

        <AiPanel
          className={pane === 'ai' ? 'mobile-show' : ''}
          entries={entries}
          settings={settings}
          vaultKey={key}
          onOpenSettings={() => setAiSettingsOpen(true)}
          onEntriesChanged={reload}
          onError={(m) => showToast(m, 'error')}
          onCopy={(t) => { void copyText(t) }}
        />
      </div>

      <div className="bottom-nav">
        <button className={pane !== 'ai' ? 'active' : ''} onClick={() => setPane('list')}>密码</button>
        <button className={pane === 'ai' ? 'active' : ''} onClick={() => setPane('ai')}>AI</button>
        <button onClick={() => { setGenTarget('modal'); setShowGen(true) }}>生成器</button>
        <button onClick={() => doBackup().catch((e) => showToast(e.message, 'error'))}>备份</button>
      </div>

      {showGroups && (
        <>
          <div className="drawer-backdrop" onClick={() => setShowGroups(false)} />
          <div className="drawer">{groupsUi}</div>
        </>
      )}

      {showGen && (
        <PasswordGenModal
          onClose={() => setShowGen(false)}
          onUse={(pwd) => {
            if (genTarget === 'form' && editing) setEditing({ ...editing, password: pwd })
            else copyText(pwd)
            setShowGen(false)
          }}
        />
      )}

      {about && (
        <div className="modal-overlay" onClick={() => setAbout(null)}>
          <div className="modal-box" onClick={(e) => e.stopPropagation()} style={{ textAlign: 'center' }}>
            <h2>{about.name}</h2>
            <div style={{ color: '#888', marginBottom: 8 }}>v{about.version}</div>
            <p>{about.description}</p>
            <div style={{ color: '#aaa', margin: '12px 0' }}>{about.author}</div>
            <button className="btn-primary" onClick={() => setAbout(null)}>确定</button>
          </div>
        </div>
      )}

      {aiSettingsOpen && settings && (
        <AiSettingsModal
          settings={settings}
          onClose={() => setAiSettingsOpen(false)}
          onSaved={(s) => { setSettings(s); setAiSettingsOpen(false); showToast('AI 设置已保存') }}
        />
      )}

      {groupEditor && (
        <div className="modal-overlay" onClick={() => setGroupEditor(null)}>
          <div className="modal-box" onClick={(e) => e.stopPropagation()}>
            <h2>{groupEditor.id ? '编辑分组' : '新建分组'}</h2>
            <Field label="名称"><input value={groupEditor.name || ''} onChange={(e) => setGroupEditor({ ...groupEditor, name: e.target.value })} /></Field>
            <Field label="描述"><input value={groupEditor.description || ''} onChange={(e) => setGroupEditor({ ...groupEditor, description: e.target.value })} /></Field>
            <Field label="颜色"><input type="color" value={groupEditor.color || '#18A058'} onChange={(e) => setGroupEditor({ ...groupEditor, color: e.target.value })} /></Field>
            <div className="form-actions">
              <button className="btn-primary" onClick={() => saveGroup()}>保存</button>
              {groupEditor.id && (
                <button className="btn-secondary btn-danger" onClick={async () => {
                  if (!confirm('删除该分组？条目将变为未分组。')) return
                  await api.deleteGroup(groupEditor.id!)
                  if (groupId === groupEditor.id) setGroupId('all')
                  setGroupEditor(null)
                  await reload()
                }}>删除</button>
              )}
              <button className="btn-secondary" onClick={() => setGroupEditor(null)}>取消</button>
            </div>
          </div>
        </div>
      )}

      {toast && <div className={`toast ${toast.type}`}>{toast.msg}</div>}
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="form-row">
      <label>{label}</label>
      {children}
    </div>
  )
}

function FieldView({ label, value, onCopy }: { label: string; value: string; onCopy?: (v: string) => void }) {
  if (!value) return null
  return (
    <div className="field-group">
      <div className="field-label">{label}</div>
      <div className="field-value pwd-field">
        <span>{value}</span>
        {onCopy && <button className="copy-btn" onClick={() => onCopy(value)}>复制</button>}
      </div>
    </div>
  )
}

function formatTime(iso: string) {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

function PasswordGenModal({ onClose, onUse }: { onClose: () => void; onUse: (pwd: string) => void }) {
  const [length, setLength] = useState(16)
  const [upper, setUpper] = useState(true)
  const [lower, setLower] = useState(true)
  const [digits, setDigits] = useState(true)
  const [symbols, setSymbols] = useState(true)
  const [nonce, setNonce] = useState(0)

  const pwd = useMemo(() => {
    void nonce
    try {
      return generatePassword({ length, upper, lower, digits, symbols })
    } catch {
      return ''
    }
  }, [length, upper, lower, digits, symbols, nonce])

  const strength = checkStrength(pwd)

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-box" onClick={(e) => e.stopPropagation()}>
        <h2>密码生成器</h2>
        <div className="pwd-display">{pwd || '点击生成'}</div>
        <div className="slider-row">
          <label>长度</label>
          <input type="range" min={4} max={128} value={length} onChange={(e) => setLength(Number(e.target.value))} />
          <span>{length}</span>
        </div>
        <div className="checks">
          <label><input type="checkbox" checked={upper} onChange={(e) => setUpper(e.target.checked)} /> 大写 A-Z</label>
          <label><input type="checkbox" checked={lower} onChange={(e) => setLower(e.target.checked)} /> 小写 a-z</label>
          <label><input type="checkbox" checked={digits} onChange={(e) => setDigits(e.target.checked)} /> 数字 0-9</label>
          <label><input type="checkbox" checked={symbols} onChange={(e) => setSymbols(e.target.checked)} /> 符号 !@#...</label>
        </div>
        <div className="strength-bar">
          <div className="bar-track"><div className="bar-fill" style={{ width: strength.width, background: strength.color }} /></div>
          <div className="bar-label" style={{ color: strength.color }}>密码强度：{strength.label}</div>
        </div>
        <div className="form-actions">
          <button className="btn-primary" onClick={() => setNonce((n) => n + 1)}>生成</button>
          <button className="btn-primary" onClick={() => pwd && onUse(pwd)}>使用此密码</button>
          <button className="btn-secondary" onClick={onClose}>关闭</button>
        </div>
      </div>
    </div>
  )
}

function AiSettingsModal({
  settings,
  onClose,
  onSaved,
}: {
  settings: Settings
  onClose: () => void
  onSaved: (s: Settings) => void
}) {
  const [form, setForm] = useState(settings)
  const [test, setTest] = useState('')

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-box" onClick={(e) => e.stopPropagation()}>
        <h2>AI 设置</h2>
        <Field label="API 地址"><input value={form.aiApiEndpoint} onChange={(e) => setForm({ ...form, aiApiEndpoint: e.target.value })} /></Field>
        <Field label="API 密钥"><input type="password" value={form.aiApiKey} onChange={(e) => setForm({ ...form, aiApiKey: e.target.value })} /></Field>
        <Field label="模型"><input value={form.aiModel} onChange={(e) => setForm({ ...form, aiModel: e.target.value })} /></Field>
        <Field label="最大 Token"><input type="number" value={form.aiMaxTokens} onChange={(e) => setForm({ ...form, aiMaxTokens: Number(e.target.value) })} /></Field>
        <Field label="温度 (0-2)"><input type="number" step="0.1" value={form.aiTemperature} onChange={(e) => setForm({ ...form, aiTemperature: Number(e.target.value) })} /></Field>
        {test && <div style={{ marginBottom: 8 }}>{test}</div>}
        <div className="form-actions">
          <button className="btn-secondary" onClick={async () => {
            setTest('测试中...')
            const r = await api.testAi({ apiEndpoint: form.aiApiEndpoint, apiKey: form.aiApiKey, model: form.aiModel })
            setTest(r.success ? '连接成功' : `失败: ${r.error}`)
          }}>测试连接</button>
          <button className="btn-primary" onClick={async () => onSaved(await api.saveSettings(form))}>保存</button>
          <button className="btn-secondary" onClick={onClose}>取消</button>
        </div>
      </div>
    </div>
  )
}

function AiPanel({
  className,
  entries,
  settings,
  vaultKey,
  onOpenSettings,
  onEntriesChanged,
  onError,
  onCopy,
}: {
  className?: string
  entries: Entry[]
  settings: Settings | null
  vaultKey: CryptoKey
  onOpenSettings: () => void
  onEntriesChanged: () => Promise<void>
  onError: (msg: string) => void
  onCopy: (text: string) => void
}) {
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const [history, setHistory] = useState<ChatMessage[]>([])
  const [bubbles, setBubbles] = useState<{ role: 'user' | 'ai'; text: string }[]>([])

  async function send() {
    const msg = input.trim()
    if (!msg || busy) return
    if (!settings?.aiApiKey) {
      onError('请先配置 AI 设置')
      onOpenSettings()
      return
    }
    setInput('')
    setBusy(true)
    setBubbles((b) => [...b, { role: 'user', text: msg }, { role: 'ai', text: '' }])
    try {
      const reply = await runAiChat({
        userMessage: msg,
        history,
        entries,
        key: vaultKey,
        settings,
        onChunk: (chunk) => {
          setBubbles((b) => {
            const next = [...b]
            const last = next[next.length - 1]
            if (last?.role === 'ai') next[next.length - 1] = { ...last, text: last.text + chunk }
            return next
          })
        },
        onTool: (status) => {
          setBubbles((b) => {
            const next = [...b]
            const last = next[next.length - 1]
            if (last?.role === 'ai' && !last.text) next[next.length - 1] = { ...last, text: status }
            return next
          })
        },
        onEntriesChanged,
      })
      setHistory((h) => [...h, { role: 'user', content: msg }, { role: 'assistant', content: reply }])
      setBubbles((b) => {
        const next = [...b]
        const last = next[next.length - 1]
        if (last?.role === 'ai' && !last.text && reply) next[next.length - 1] = { ...last, text: reply }
        return next
      })
    } catch (e) {
      const message = e instanceof Error ? e.message : 'AI 请求失败'
      setBubbles((b) => {
        const next = [...b]
        const last = next[next.length - 1]
        if (last?.role === 'ai') next[next.length - 1] = { ...last, text: message }
        return next
      })
      onError(message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={`col col-ai ${className || ''}`}>
      <div className="chat-header">
        <span className="title">AI 助手</span>
        <div className="chat-header-right">
          <button className="icon-btn" onClick={onOpenSettings}>设置</button>
          <button className="icon-btn" onClick={() => { setBubbles([]); setHistory([]) }}>清空</button>
        </div>
      </div>
      <div className="chat-messages">
        {bubbles.length === 0 && (
          <div className="chat-welcome">
            <div>你好！我是密码管家 AI 助手</div>
            <div>用自然语言管理你的密码</div>
            <div style={{ marginTop: 8, textAlign: 'left', display: 'inline-block', fontSize: 12, color: '#ccc' }}>
              • 帮我查看 GitHub 的密码<br />
              • 添加一个新的 Gmail 密码<br />
              • 生成一个16位的随机密码
            </div>
          </div>
        )}
        {bubbles.map((b, i) => (
          <div key={i} className={`chat-msg ${b.role === 'user' ? 'user' : 'ai'}`}>
            <div
              className="chat-bubble"
              onClick={(e) => {
                const el = e.target as HTMLElement
                if (el.dataset.pwd) onCopy(el.dataset.pwd)
              }}
              dangerouslySetInnerHTML={{ __html: formatAi(b.text) }}
            />
          </div>
        ))}
      </div>
      <div className="chat-input-area">
        <input
          value={input}
          placeholder="输入消息..."
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') send() }}
        />
        <button disabled={busy} onClick={send}>发送</button>
      </div>
    </div>
  )
}

function formatAi(text: string) {
  const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
  let html = esc(text)
  html = html.replace(/\[PASSWORD:(.*?)]/g, (_m, pwd) => `•••••••• <button class="copy-btn" data-pwd="${esc(pwd)}">复制</button>`)
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>')
  html = html.replace(/\n/g, '<br>')
  return html
}
