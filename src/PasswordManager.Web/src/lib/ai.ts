import type { ItemType, VaultDoc } from '../types'
import { api } from './api'
import { generatePassword } from './password'
import { cloneVault, emptyAccount, emptyItem, findItemByKey, nowIso } from './vault'

const SYSTEM_PROMPT = `你是"凭据管理器"AI 助手，帮助用户管理他们的登录账号、凭据、密钥和备忘。

## 数据模型
- 一个「条目」对应一个网站/服务（如 GitHub、QQ），可包含多个账号
- 同一网址下的多个账号属于同一个条目（例如 8 个 GitHub 账号）
- 条目类型：login（登录账号）、credential（凭据密码）、key（密钥/Key）、note（备忘）

## 你的能力
- 搜索、查看、添加、修改、删除条目和账号
- 生成安全的随机密码
- 列出所有分类

## 回复规则
- 使用中文回复
- 回复简洁明了
- 删除前提醒用户确认
- 展示密码/密钥时使用：[PASSWORD:实际内容]
- 除此之外不要在回复中出现任何密码明文
- 自定义字段中的隐藏字段显示为 ••••••••

## 查找
- 用户要求查看某个密码时，先用 search_vault 搜索
- 不要要求用户提供精确标题`

export const AI_TOOLS = [
  {
    type: 'function',
    function: {
      name: 'search_vault',
      description: '搜索凭据库。按标题、网址、用户名、分类、账号备注搜索。',
      parameters: {
        type: 'object',
        properties: { keyword: { type: 'string', description: '搜索关键词' } },
        required: ['keyword'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'get_item',
      description: '获取指定条目的详细信息（含全部账号）。按标题或网址匹配。',
      parameters: {
        type: 'object',
        properties: {
          title: { type: 'string', description: '条目标题' },
          url: { type: 'string', description: '网址' },
        },
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'add_account',
      description: '添加账号。若已有相同网址或标题的条目，会把账号加到该条目下（支持同一网站多个账号）。否则新建条目。',
      parameters: {
        type: 'object',
        properties: {
          title: { type: 'string', description: '条目标题，如 GitHub、QQ' },
          url: { type: 'string' },
          type: { type: 'string', description: 'login | credential | key | note' },
          label: { type: 'string', description: '账号别名，如 工作号、个人号' },
          username: { type: 'string' },
          secret: { type: 'string', description: '密码、凭据或密钥内容' },
          notes: { type: 'string' },
          category: { type: 'string' },
        },
        required: ['title'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'delete_item',
      description: '删除整个条目（含其下全部账号）。',
      parameters: {
        type: 'object',
        properties: { id: { type: 'string' } },
        required: ['id'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'list_categories',
      description: '列出所有分类。',
      parameters: { type: 'object', properties: {} },
    },
  },
  {
    type: 'function',
    function: {
      name: 'generate_password',
      description: '生成安全的随机密码。',
      parameters: {
        type: 'object',
        properties: {
          length: { type: 'integer', description: '密码长度，默认16' },
          includeSymbols: { type: 'boolean' },
        },
      },
    },
  },
]

type ChatMessage = Record<string, unknown>

export async function runAiChat(options: {
  userMessage: string
  history: ChatMessage[]
  vault: VaultDoc
  persist: (vault: VaultDoc) => Promise<void>
  settings: { aiModel: string; aiMaxTokens: number; aiTemperature: number }
  onChunk: (text: string) => void
  onTool: (status: string) => void
  onVaultChanged: () => Promise<void>
}): Promise<string> {
  const messages: ChatMessage[] = [
    { role: 'system', content: SYSTEM_PROMPT },
    ...options.history.slice(-20),
    { role: 'user', content: options.userMessage },
  ]

  let vault = options.vault
  let full = ''

  for (let round = 0; round < 6; round++) {
    const result = await streamCompletion(messages, options)
    full += result.text
    if (result.toolCalls.length === 0) break

    messages.push({
      role: 'assistant',
      content: result.text || null,
      tool_calls: result.toolCalls.map((tc) => ({
        id: tc.id,
        type: 'function',
        function: { name: tc.name, arguments: tc.arguments },
      })),
    })

    for (const tc of result.toolCalls) {
      options.onTool(`正在执行: ${toolLabel(tc.name)}...`)
      const { output, vault: nextVault } = await executeTool(tc.name, tc.arguments, vault, options.persist)
      vault = nextVault
      if (tc.name === 'add_account' || tc.name === 'delete_item') {
        await options.onVaultChanged()
      }
      messages.push({ role: 'tool', tool_call_id: tc.id, content: output })
    }
  }

  return full
}

type ToolCall = { id: string; name: string; arguments: string }

async function streamCompletion(
  messages: ChatMessage[],
  options: {
    settings: { aiModel: string; aiMaxTokens: number; aiTemperature: number }
    onChunk: (text: string) => void
  },
): Promise<{ text: string; toolCalls: ToolCall[] }> {
  const res = await api.aiCompletions({
    model: options.settings.aiModel,
    messages,
    tools: AI_TOOLS,
    max_tokens: options.settings.aiMaxTokens,
    temperature: options.settings.aiTemperature,
    stream: true,
  })

  const reader = res.body?.getReader()
  if (!reader) throw new Error('无法读取 AI 响应')

  const decoder = new TextDecoder()
  let buffer = ''
  let text = ''
  const tools = new Map<number, ToolCall>()

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n')
    buffer = lines.pop() ?? ''

    for (const line of lines) {
      const trimmed = line.trim()
      if (!trimmed.startsWith('data:')) continue
      const data = trimmed.slice(5).trim()
      if (data === '[DONE]') continue
      try {
        const json = JSON.parse(data)
        const delta = json.choices?.[0]?.delta
        if (!delta) continue
        if (typeof delta.content === 'string' && delta.content) {
          text += delta.content
          options.onChunk(delta.content)
        }
        if (Array.isArray(delta.tool_calls)) {
          for (const tc of delta.tool_calls) {
            const idx = tc.index ?? 0
            const current = tools.get(idx) ?? { id: '', name: '', arguments: '' }
            if (tc.id) current.id = tc.id
            if (tc.function?.name) current.name += tc.function.name
            if (tc.function?.arguments) current.arguments += tc.function.arguments
            tools.set(idx, current)
          }
        }
      } catch {
        /* ignore incomplete JSON */
      }
    }
  }

  return { text, toolCalls: [...tools.values()] }
}

async function executeTool(
  name: string,
  argsJson: string,
  vault: VaultDoc,
  persist: (next: VaultDoc) => Promise<void>,
): Promise<{ output: string; vault: VaultDoc }> {
  let args: Record<string, unknown> = {}
  try {
    args = JSON.parse(argsJson || '{}')
  } catch {
    return { output: JSON.stringify({ error: '参数无法解析' }), vault }
  }

  switch (name) {
    case 'search_vault': {
      const keyword = String(args.keyword ?? '').toLowerCase()
      const results = vault.items
        .filter((item) => matches(item, keyword))
        .map((item) => ({
          id: item.id,
          title: item.title,
          type: item.type,
          url: item.url,
          category: item.category,
          accounts: item.accounts.map((a) => ({ label: a.label, username: a.username })),
        }))
      return { output: JSON.stringify({ count: results.length, results }), vault }
    }
    case 'get_item': {
      const title = String(args.title ?? '')
      const url = String(args.url ?? '')
      const item = vault.items.find((i) =>
        (url && i.url.toLowerCase().includes(url.toLowerCase()))
        || (title && i.title.toLowerCase() === title.toLowerCase()),
      ) || vault.items.find((i) => title && i.title.toLowerCase().includes(title.toLowerCase()))
      if (!item) return { output: JSON.stringify({ error: `未找到 ${title || url}` }), vault }
      return {
        output: JSON.stringify({
          id: item.id,
          title: item.title,
          type: item.type,
          url: item.url,
          category: item.category,
          notes: item.notes,
          accounts: item.accounts.map((a) => ({
            id: a.id,
            label: a.label,
            username: a.username,
            secret: a.secret,
            notes: a.notes,
            fields: a.fields.map((f) => ({
              key: f.key,
              value: f.isHidden ? '••••••••' : f.value,
              isHidden: f.isHidden,
            })),
          })),
        }),
        vault,
      }
    }
    case 'add_account': {
      const next = cloneVault(vault)
      const title = String(args.title ?? '').trim()
      const url = String(args.url ?? '')
      const type = (['login', 'credential', 'key', 'note'].includes(String(args.type))
        ? String(args.type)
        : 'login') as ItemType
      let item = findItemByKey(next, url, title)
      if (!item) {
        item = emptyItem()
        item.title = title || '未命名'
        item.url = url
        item.type = type
        item.category = String(args.category ?? '')
        item.accounts = []
        next.items.push(item)
      }
      const acc = emptyAccount()
      acc.label = String(args.label ?? args.username ?? '默认')
      acc.username = String(args.username ?? '')
      acc.secret = String(args.secret ?? args.password ?? '')
      acc.notes = String(args.notes ?? '')
      item.accounts.push(acc)
      item.updatedAt = nowIso()
      await persist(next)
      return {
        output: JSON.stringify({
          success: true,
          itemId: item.id,
          accountId: acc.id,
          message: `已将账号加入「${item.title}」，当前共 ${item.accounts.length} 个账号`,
        }),
        vault: next,
      }
    }
    case 'delete_item': {
      const id = String(args.id ?? '')
      const next = cloneVault(vault)
      next.items = next.items.filter((i) => i.id !== id)
      await persist(next)
      return { output: JSON.stringify({ success: true, message: '已删除' }), vault: next }
    }
    case 'list_categories': {
      const categories = [...new Set(vault.items.map((e) => e.category).filter(Boolean))]
      return { output: JSON.stringify({ categories }), vault }
    }
    case 'generate_password': {
      const length = Number(args.length ?? 16)
      const includeSymbols = args.includeSymbols !== false
      const password = generatePassword({
        length: Math.min(128, Math.max(8, length)),
        upper: true,
        lower: true,
        digits: true,
        symbols: includeSymbols,
      })
      return { output: JSON.stringify({ password }), vault }
    }
    default:
      return { output: JSON.stringify({ error: `未知工具: ${name}` }), vault }
  }
}

function matches(item: VaultDoc['items'][number], keyword: string) {
  if (!keyword) return true
  const hay = [
    item.title,
    item.url,
    item.category,
    item.notes,
    ...item.accounts.flatMap((a) => [a.label, a.username, a.notes]),
  ]
  return hay.some((v) => (v || '').toLowerCase().includes(keyword))
}

function toolLabel(name: string) {
  const map: Record<string, string> = {
    search_vault: '搜索凭据',
    get_item: '查看条目',
    add_account: '添加账号',
    delete_item: '删除条目',
    list_categories: '列出分类',
    generate_password: '生成密码',
  }
  return map[name] ?? name
}

export type { ChatMessage }
