import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../lib/api'
import { runAiChat, type ChatMessage } from '../lib/ai'
import { checkStrength, generatePassword } from '../lib/password'
import {
  decryptDetected,
  decryptDetectedVault,
  detectImport,
  mergeEntries,
  mergeVaultDocs,
  type DetectedImport,
} from '../lib/import'
import {
  cloneVault,
  decryptVault,
  emptyAccount,
  emptyGroup,
  emptyItem,
  emptyVault,
  encryptVault,
  newId,
  normalizeUrl,
  nowIso,
  parseVault,
} from '../lib/vault'
import { AccountMenu } from '../components/AccountMenu'
import { useSession } from '../context/SessionContext'
import type { AboutInfo, Account, Group, ItemType, Settings, VaultDoc, VaultItem } from '../types'
import { ITEM_TYPES } from '../types'

type Toast = { msg: string; type: 'success' | 'error' }
type MobilePane = 'list' | 'detail' | 'ai'

export function VaultPage() {
  const { session } = useSession()
  const key = session!.key

  const [vault, setVault] = useState<VaultDoc>(emptyVault())
  const [settings, setSettings] = useState<Settings | null>(null)
  const [keyword, setKeyword] = useState('')
  const [groupId, setGroupId] = useState<string | 'all'>('all')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [editing, setEditing] = useState<VaultItem | null>(null)
  const [toast, setToast] = useState<Toast | null>(null)
  const [pane, setPane] = useState<MobilePane>('list')
  const [showGroups, setShowGroups] = useState(false)
  const [showGen, setShowGen] = useState(false)
  const [genAccountId, setGenAccountId] = useState<string | null>(null)
  const [about, setAbout] = useState<AboutInfo | null>(null)
  const [aiSettingsOpen, setAiSettingsOpen] = useState(false)
  const [groupEditor, setGroupEditor] = useState<Partial<Group> | null>(null)
  const [showImport, setShowImport] = useState(false)
  const [revealed, setRevealed] = useState<Record<string, boolean>>({})

  const groups = vault.groups
  const items = vault.items
  const selected = items.find((i) => i.id === selectedId) ?? null

  const showToast = (msg: string, type: Toast['type'] = 'success') => {
    setToast({ msg, type })
    setTimeout(() => setToast(null), 2500)
  }

  const persist = useCallback(async (next: VaultDoc) => {
    const encrypted = await encryptVault(key, next)
    await api.saveVault(encrypted)
    setVault(next)
  }, [key])

  const reload = useCallback(async () => {
    const { document } = await api.getVault()
    setVault(await decryptVault(key, parseVault(document)))
  }, [key])

  useEffect(() => {
    reload().catch((e) => showToast(e.message, 'error'))
    api.getSettings().then(setSettings).catch(() => undefined)
  }, [reload])

  const filtered = useMemo(() => {
    const q = keyword.trim().toLowerCase()
    return items.filter((item) => {
      if (groupId === '' && item.groupId) return false
      if (groupId !== 'all' && groupId !== '' && (item.groupId || '') !== groupId) return false
      if (!q) return true
      const hay = [
        item.title,
        item.url,
        item.category,
        item.notes,
        item.type,
        ...item.accounts.flatMap((a) => [a.label, a.username, a.notes]),
      ]
      return hay.some((v) => (v || '').toLowerCase().includes(q))
    })
  }, [items, keyword, groupId])

  function openItem(id: string) {
    setSelectedId(id)
    setEditing(null)
    setRevealed({})
    setPane('detail')
  }

  function startAdd() {
    const draft = emptyItem()
    draft.groupId = groupId === 'all' || groupId === '' ? null : groupId
    setSelectedId(null)
    setEditing(draft)
    setPane('detail')
  }

  function startEdit() {
    if (!selected) return
    setEditing(cloneVault({ version: '4.0', groups: [], items: [selected] }).items[0]!)
  }

  function addAccountToSelected() {
    if (!selected) return
    const draft = cloneVault({ version: '4.0', groups: [], items: [selected] }).items[0]!
    draft.accounts.push(emptyAccount())
    setEditing(draft)
  }

  async function saveItem() {
    if (!editing?.title.trim()) {
      showToast('请输入标题', 'error')
      return
    }
    if (editing.accounts.length === 0) {
      showToast('至少保留一个账号', 'error')
      return
    }
    const draft: VaultItem = {
      ...editing,
      title: editing.title.trim(),
      accounts: editing.accounts.map((a) => ({ ...a, id: a.id || newId() })),
      updatedAt: nowIso(),
    }
    const isNew = !items.some((i) => i.id === draft.id)
    const next = cloneVault(vault)
    const url = normalizeUrl(draft.url)
    const match = url
      ? next.items.find((i) => i.id !== draft.id && normalizeUrl(i.url) === url)
      : undefined

    let savedId = draft.id
    let merged = false
    if (match) {
      match.accounts.push(...draft.accounts)
      match.updatedAt = nowIso()
      if (!isNew) next.items = next.items.filter((i) => i.id !== draft.id)
      savedId = match.id
      merged = true
    } else if (isNew) {
      next.items.push(draft)
    } else {
      next.items = next.items.map((i) => (i.id === draft.id ? draft : i))
    }

    try {
      await persist(next)
      setSelectedId(savedId)
      setEditing(null)
      showToast(merged ? `已合并到「${match!.title}」，当前 ${match!.accounts.length} 个账号` : (isNew ? '添加成功' : '更新成功'))
    } catch (e) {
      showToast(e instanceof Error ? e.message : '保存失败', 'error')
    }
  }

  async function removeItem() {
    if (!selectedId || !confirm('确定删除这个条目及其全部账号？')) return
    const next = cloneVault(vault)
    next.items = next.items.filter((i) => i.id !== selectedId)
    await persist(next)
    setSelectedId(null)
    setEditing(null)
    setPane('list')
    showToast('已删除')
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
    a.download = `vault-backup-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
    showToast('备份已下载')
  }

  async function saveGroup() {
    if (!groupEditor?.name?.trim()) return
    const next = cloneVault(vault)
    if (groupEditor.id) {
      next.groups = next.groups.map((g) =>
        g.id === groupEditor.id
          ? {
              ...g,
              name: groupEditor.name!.trim(),
              description: groupEditor.description || '',
              color: groupEditor.color || g.color,
              updatedAt: nowIso(),
            }
          : g,
      )
    } else {
      const g = emptyGroup(groupEditor.name.trim())
      g.color = groupEditor.color || g.color
      g.description = groupEditor.description || ''
      g.sortOrder = next.groups.length
      next.groups.push(g)
    }
    await persist(next)
    setGroupEditor(null)
  }

  const typeLabel = (type: ItemType) => ITEM_TYPES.find((t) => t.id === type)?.label ?? type
  const secretLabel = (type: ItemType) => ITEM_TYPES.find((t) => t.id === type)?.secretLabel ?? '密码'

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
        <div className="toolbar-title"><span>凭据管理器</span></div>
        <div className="toolbar-right">
          <button className="hide-sm" onClick={() => { setGenAccountId(null); setShowGen(true) }}>密码生成器</button>
          <button className="hide-sm" onClick={() => api.about().then(setAbout)}>关于</button>
          <button className="hide-sm" onClick={() => setShowImport(true)}>导入</button>
          <button className="hide-sm" onClick={() => doBackup().catch((e) => showToast(e.message, 'error'))}>备份</button>
          <AccountMenu />
        </div>
      </div>

      <div className="columns" data-pane={pane} style={{ position: 'relative' }}>
        <div className="col col-groups desktop-only">{groupsUi}</div>

        <div className="col col-list">
          <div className="search-box">
            <input placeholder="搜索标题、网址、用户名..." value={keyword} onChange={(e) => setKeyword(e.target.value)} />
          </div>
          <div className="list-count">{filtered.length} 个条目</div>
          <div className="scroll">
            {filtered.map((item) => (
              <div
                key={item.id}
                className={`entry-item ${item.id === selectedId ? 'active' : ''}`}
                onClick={() => openItem(item.id)}
              >
                <div className="entry-title">{item.title}</div>
                <div className="entry-meta">
                  {item.accounts.length > 1
                    ? `${item.accounts.length} 个账号`
                    : (item.accounts[0]?.username || item.accounts[0]?.label || typeLabel(item.type))}
                  {item.url ? ` · ${hostOf(item.url)}` : ''}
                </div>
                <span className="entry-category">{typeLabel(item.type)}</span>
                {item.category && <span className="entry-category" style={{ marginLeft: 4 }}>{item.category}</span>}
              </div>
            ))}
          </div>
          <button className="add-btn" onClick={startAdd}>+ 添加凭据</button>
        </div>

        <div className={`col col-detail ${pane === 'detail' ? 'mobile-show' : ''}`}>
          {editing ? (
            <ItemEditor
              item={editing}
              groups={groups}
              onChange={setEditing}
              onSave={() => void saveItem()}
              onCancel={() => { setEditing(null); if (!selectedId) setPane('list') }}
              onGenerate={(accountId) => { setGenAccountId(accountId); setShowGen(true) }}
            />
          ) : selected ? (
            <div className="detail-view">
              <div className="detail-header">
                <button className="mobile-only icon-btn" onClick={() => setPane('list')}>←</button>
                <span className="title">{selected.title}</span>
                <span className="entry-category">{typeLabel(selected.type)}</span>
                {selected.category && <span className="entry-category">{selected.category}</span>}
                <div className="detail-actions">
                  <button className="icon-btn" onClick={addAccountToSelected}>加账号</button>
                  <button className="icon-btn" onClick={startEdit}>编辑</button>
                  <button className="icon-btn btn-danger" onClick={() => void removeItem()}>删除</button>
                </div>
              </div>
              {selected.url && (
                <div className="field-group">
                  <div className="field-label">网址</div>
                  <div className="field-value"><a href={selected.url} target="_blank" rel="noreferrer">{selected.url}</a></div>
                </div>
              )}
              {selected.notes && <FieldView label="条目备注" value={selected.notes} />}
              {selected.accounts.map((acc, idx) => (
                <div key={acc.id} className="account-card">
                  <div className="account-card-title">
                    {acc.label || `账号 ${idx + 1}`}
                    {selected.accounts.length > 1 && <span className="entry-meta"> · {idx + 1}/{selected.accounts.length}</span>}
                  </div>
                  <FieldView label="用户名" value={acc.username} onCopy={copyText} />
                  <div className="field-group">
                    <div className="field-label">{secretLabel(selected.type)}</div>
                    <div className="field-value pwd-field">
                      <span>{revealed[acc.id] ? acc.secret : '••••••••'}</span>
                      <button className="copy-btn" onClick={() => setRevealed((r) => ({ ...r, [acc.id]: !r[acc.id] }))}>
                        {revealed[acc.id] ? '隐藏' : '显示'}
                      </button>
                      <button className="copy-btn" onClick={() => void copyText(acc.secret)}>复制</button>
                    </div>
                  </div>
                  {acc.notes && <FieldView label="备注" value={acc.notes} />}
                  {acc.fields.map((f, i) => (
                    <FieldView
                      key={i}
                      label={f.key}
                      value={f.isHidden ? '••••••••' : f.value}
                      onCopy={f.isHidden ? () => copyText(f.value) : undefined}
                    />
                  ))}
                </div>
              ))}
              <div style={{ marginTop: 20, fontSize: 11, color: '#bbb' }}>
                创建：{formatTime(selected.createdAt)}　|　更新：{formatTime(selected.updatedAt)}
              </div>
            </div>
          ) : (
            <div className="detail-empty">
              <div>选择一个条目查看详情</div>
              <div style={{ fontSize: 12 }}>同一网址可保存多个账号，例如多个 GitHub / QQ</div>
            </div>
          )}
        </div>

        <AiPanel
          className={pane === 'ai' ? 'mobile-show' : ''}
          vault={vault}
          persist={persist}
          settings={settings}
          onOpenSettings={() => setAiSettingsOpen(true)}
          onVaultChanged={reload}
          onError={(m) => showToast(m, 'error')}
          onCopy={(t) => { void copyText(t) }}
        />
      </div>

      <div className="bottom-nav">
        <button className={pane !== 'ai' ? 'active' : ''} onClick={() => setPane('list')}>凭据</button>
        <button className={pane === 'ai' ? 'active' : ''} onClick={() => setPane('ai')}>AI</button>
        <button onClick={() => { setGenAccountId(null); setShowGen(true) }}>生成器</button>
        <button onClick={() => setShowImport(true)}>导入</button>
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
            if (genAccountId && editing) {
              setEditing({
                ...editing,
                accounts: editing.accounts.map((a) => a.id === genAccountId ? { ...a, secret: pwd } : a),
              })
            } else {
              void copyText(pwd)
            }
            setShowGen(false)
          }}
        />
      )}

      {showImport && (
        <ImportModal
          vaultKey={key}
          vault={vault}
          onClose={() => setShowImport(false)}
          onDone={async (next, msg) => {
            await persist(next)
            setShowImport(false)
            showToast(msg)
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
              <button className="btn-primary" onClick={() => void saveGroup()}>保存</button>
              {groupEditor.id && (
                <button className="btn-secondary btn-danger" onClick={async () => {
                  if (!confirm('删除该分组？条目将变为未分组。')) return
                  const next = cloneVault(vault)
                  next.groups = next.groups.filter((g) => g.id !== groupEditor.id)
                  next.items = next.items.map((i) => i.groupId === groupEditor.id ? { ...i, groupId: null } : i)
                  await persist(next)
                  if (groupId === groupEditor.id) setGroupId('all')
                  setGroupEditor(null)
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

function ItemEditor({
  item,
  groups,
  onChange,
  onSave,
  onCancel,
  onGenerate,
}: {
  item: VaultItem
  groups: Group[]
  onChange: (item: VaultItem) => void
  onSave: () => void
  onCancel: () => void
  onGenerate: (accountId: string) => void
}) {
  const secretName = ITEM_TYPES.find((t) => t.id === item.type)?.secretLabel ?? '密码'
  function patchAccount(id: string, patch: Partial<Account>) {
    onChange({ ...item, accounts: item.accounts.map((a) => a.id === id ? { ...a, ...patch } : a) })
  }

  return (
    <div className="edit-form">
      <div className="detail-header">
        <button className="mobile-only icon-btn" type="button" onClick={onCancel}>←</button>
        <span className="title">{item.title ? '编辑凭据' : '添加凭据'}</span>
      </div>
      <Field label="类型">
        <select value={item.type} onChange={(e) => onChange({ ...item, type: e.target.value as ItemType })}>
          {ITEM_TYPES.map((t) => <option key={t.id} value={t.id}>{t.label}</option>)}
        </select>
      </Field>
      <Field label="标题 *"><input value={item.title} onChange={(e) => onChange({ ...item, title: e.target.value })} placeholder="如 GitHub、QQ、某 API Key" /></Field>
      <Field label="网址"><input value={item.url} onChange={(e) => onChange({ ...item, url: e.target.value })} placeholder="相同网址会自动合并为多账号" /></Field>
      <Field label="分类"><input value={item.category} onChange={(e) => onChange({ ...item, category: e.target.value })} placeholder="如：邮箱、社交、开发工具" /></Field>
      <Field label="分组">
        <select value={item.groupId || ''} onChange={(e) => onChange({ ...item, groupId: e.target.value || null })}>
          <option value="">未分组</option>
          {groups.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
        </select>
      </Field>
      <Field label="条目备注"><textarea value={item.notes} onChange={(e) => onChange({ ...item, notes: e.target.value })} /></Field>

      {item.accounts.map((acc, idx) => (
        <div key={acc.id} className="account-card">
          <div className="account-card-title">
            账号 {idx + 1}
            {item.accounts.length > 1 && (
              <button
                className="icon-btn btn-danger"
                type="button"
                onClick={() => onChange({ ...item, accounts: item.accounts.filter((a) => a.id !== acc.id) })}
              >
                删除
              </button>
            )}
          </div>
          <Field label="账号别名"><input value={acc.label} onChange={(e) => patchAccount(acc.id, { label: e.target.value })} placeholder="工作号 / 个人号 / 默认" /></Field>
          <Field label="用户名"><input value={acc.username} onChange={(e) => patchAccount(acc.id, { username: e.target.value })} /></Field>
          <Field label={secretName}>
            <div className="secret-row">
              <input value={acc.secret} onChange={(e) => patchAccount(acc.id, { secret: e.target.value })} />
              <button className="btn-secondary" type="button" onClick={() => onGenerate(acc.id)}>生成</button>
            </div>
          </Field>
          <Field label="备注"><textarea value={acc.notes} onChange={(e) => patchAccount(acc.id, { notes: e.target.value })} /></Field>
          <div className="form-row">
            <label>自定义字段（不规则信息可放这里）</label>
            {acc.fields.map((f, i) => (
              <div key={i} className="custom-field-row">
                <input placeholder="名称" value={f.key} onChange={(e) => {
                  const fields = [...acc.fields]; fields[i] = { ...f, key: e.target.value }; patchAccount(acc.id, { fields })
                }} />
                <input placeholder="值" value={f.value} onChange={(e) => {
                  const fields = [...acc.fields]; fields[i] = { ...f, value: e.target.value }; patchAccount(acc.id, { fields })
                }} />
                <label className="custom-field-hide">
                  <input type="checkbox" checked={f.isHidden} onChange={(e) => {
                    const fields = [...acc.fields]; fields[i] = { ...f, isHidden: e.target.checked }; patchAccount(acc.id, { fields })
                  }} /> 隐藏
                </label>
                <button className="btn-secondary" type="button" onClick={() => patchAccount(acc.id, { fields: acc.fields.filter((_, j) => j !== i) })}>删</button>
              </div>
            ))}
            <button className="btn-secondary" type="button" onClick={() => patchAccount(acc.id, { fields: [...acc.fields, { key: '', value: '', isHidden: false }] })}>+ 字段</button>
          </div>
        </div>
      ))}

      <button className="btn-secondary add-account-btn" type="button" onClick={() => onChange({ ...item, accounts: [...item.accounts, emptyAccount()] })}>
        + 添加账号（同一网址）
      </button>
      <div className="form-actions">
        <button className="btn-primary" onClick={onSave}>保存</button>
        <button className="btn-secondary" onClick={onCancel}>取消</button>
      </div>
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

function hostOf(url: string) {
  try {
    return new URL(url).hostname.replace(/^www\./, '')
  } catch {
    return url.replace(/^https?:\/\//, '').split('/')[0] || url
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
  vault,
  persist,
  settings,
  onOpenSettings,
  onVaultChanged,
  onError,
  onCopy,
}: {
  className?: string
  vault: VaultDoc
  persist: (next: VaultDoc) => Promise<void>
  settings: Settings | null
  onOpenSettings: () => void
  onVaultChanged: () => Promise<void>
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
        vault,
        persist,
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
        onVaultChanged,
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
            <div>你好！我是凭据管理器 AI 助手</div>
            <div>同一网站下可以有多个账号</div>
            <div style={{ marginTop: 8, textAlign: 'left', display: 'inline-block', fontSize: 12, color: '#ccc' }}>
              • 帮我查看 GitHub 的全部账号<br />
              • 给 QQ 再加一个小号<br />
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
        <button disabled={busy} onClick={() => void send()}>发送</button>
      </div>
    </div>
  )
}

function ImportModal({
  vaultKey,
  vault,
  onClose,
  onDone,
}: {
  vaultKey: CryptoKey
  vault: VaultDoc
  onClose: () => void
  onDone: (next: VaultDoc, msg: string) => Promise<void>
}) {
  const [detected, setDetected] = useState<DetectedImport | null>(null)
  const [fileName, setFileName] = useState('')
  const [legacyPassword, setLegacyPassword] = useState('')
  const [skipDuplicates, setSkipDuplicates] = useState(true)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function onFile(file: File) {
    setError('')
    setDetected(null)
    setFileName(file.name)
    try {
      const text = await file.text()
      setDetected(detectImport(text))
    } catch (e) {
      setError(e instanceof Error ? e.message : '无法解析文件')
    }
  }

  async function submit() {
    if (!detected) {
      setError('请先选择文件')
      return
    }
    setBusy(true)
    setError('')
    try {
      let result: { vault: VaultDoc; imported: number; skipped: number }
      if (detected.vault) {
        const incoming = await decryptDetectedVault(detected, vaultKey, legacyPassword)
        result = mergeVaultDocs(vault, incoming, skipDuplicates)
      } else {
        const plain = await decryptDetected(detected, vaultKey, legacyPassword)
        result = mergeEntries(vault, detected.groups, plain, skipDuplicates)
      }
      await onDone(result.vault, `导入完成：新增 ${result.imported} 个账号，跳过 ${result.skipped} 个`)
    } catch (e) {
      setError(e instanceof Error ? e.message : '导入失败')
    } finally {
      setBusy(false)
    }
  }

  const count = detected?.vault
    ? detected.vault.items.reduce((n, i) => n + i.accounts.length, 0)
    : detected?.entries.length ?? 0

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-box" onClick={(e) => e.stopPropagation()}>
        <h2>导入凭据</h2>
        <p style={{ color: '#888', marginBottom: 12, fontSize: 12, lineHeight: 1.6 }}>
          支持本应用备份 JSON、旧版本地密码库、Bitwarden 未加密 JSON，以及 Chrome / Firefox / 通用 CSV。相同网址会合并为多账号。
        </p>
        <div className="form-row">
          <label>选择文件</label>
          <input
            type="file"
            accept=".json,.csv,text/csv,application/json"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) void onFile(file)
            }}
          />
        </div>
        {fileName && detected && (
          <p style={{ marginBottom: 12 }}>
            已识别：<strong>{detected.format}</strong>，{count} 条
            {detected.groups.length ? `，${detected.groups.length} 个分组` : ''}
          </p>
        )}
        {(detected?.needsPassword || detected?.encrypted) && (
          <div className="form-row">
            <label>{detected.needsPassword ? '原主密码（必填）' : '原主密码（如备份来自其他主密码）'}</label>
            <input
              type="password"
              value={legacyPassword}
              placeholder={detected.needsPassword ? '旧版密码库的主密码' : '当前主密码可留空'}
              onChange={(e) => setLegacyPassword(e.target.value)}
            />
          </div>
        )}
        <label style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12 }}>
          <input type="checkbox" checked={skipDuplicates} onChange={(e) => setSkipDuplicates(e.target.checked)} />
          同一网址下用户名相同则跳过
        </label>
        {error && <div className="error" style={{ color: 'var(--danger)', marginBottom: 12 }}>{error}</div>}
        <div className="form-actions">
          <button className="btn-primary" disabled={busy || !detected} onClick={() => void submit()}>
            {busy ? '导入中...' : '开始导入'}
          </button>
          <button className="btn-secondary" onClick={onClose}>取消</button>
        </div>
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
