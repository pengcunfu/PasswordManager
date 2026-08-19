import type { Entry } from '../types'
import { api } from './api'
import { generatePassword } from './password'
import { encryptEntryPayload } from './vault'

const SYSTEM_PROMPT = `你是"密码管家"AI 助手，帮助用户管理他们的密码和账户信息。

## 你的能力
- 搜索、查看、添加、修改、删除密码条目
- 生成安全的随机密码
- 列出所有密码分类
- 管理自定义字段（邮箱、手机号、密保手机、密保问题等）

## 回复规则
- 使用中文回复
- 回复简洁明了
- 当需要删除密码时，提醒用户确认
- 当需要展示密码时，使用特殊格式：[PASSWORD:实际密码内容]
- 除此之外不要在回复中出现任何密码明文
- 自定义字段中的隐藏字段显示为 ••••••••

## 查找密码
- 用户要求查看某个密码时，先用 search_passwords 搜索
- 不要要求用户提供精确标题`

export const AI_TOOLS = [
  {
    type: 'function',
    function: {
      name: 'search_passwords',
      description: '搜索密码条目。根据关键词搜索标题、用户名、网址、分类。',
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
      name: 'get_password',
      description: '获取指定密码条目的详细信息。根据标题匹配。',
      parameters: {
        type: 'object',
        properties: { title: { type: 'string', description: '密码条目标题' } },
        required: ['title'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'add_password',
      description: '添加新的密码条目。',
      parameters: {
        type: 'object',
        properties: {
          title: { type: 'string' },
          username: { type: 'string' },
          password: { type: 'string' },
          url: { type: 'string' },
          notes: { type: 'string' },
          category: { type: 'string' },
        },
        required: ['title', 'username', 'password'],
      },
    },
  },
  {
    type: 'function',
    function: {
      name: 'delete_password',
      description: '删除密码条目。',
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
      description: '列出所有密码分类。',
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
  entries: Entry[]
  key: CryptoKey
  settings: { aiModel: string; aiMaxTokens: number; aiTemperature: number }
  onChunk: (text: string) => void
  onTool: (status: string) => void
  onEntriesChanged: () => Promise<void>
}): Promise<string> {
  const messages: ChatMessage[] = [
    { role: 'system', content: SYSTEM_PROMPT },
    ...options.history.slice(-20),
    { role: 'user', content: options.userMessage },
  ]

  let entries = options.entries
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
      const output = await executeTool(tc.name, tc.arguments, entries, options.key)
      if (tc.name === 'add_password' || tc.name === 'delete_password') {
        await options.onEntriesChanged()
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
  entries: Entry[],
  key: CryptoKey,
): Promise<string> {
  let args: Record<string, unknown> = {}
  try {
    args = JSON.parse(argsJson || '{}')
  } catch {
    return JSON.stringify({ error: '参数无法解析' })
  }

  switch (name) {
    case 'search_passwords': {
      const keyword = String(args.keyword ?? '').toLowerCase()
      const results = entries
        .filter((e) =>
          [e.title, e.username, e.url, e.category, e.notes].some((v) =>
            (v || '').toLowerCase().includes(keyword),
          ),
        )
        .map((e) => ({
          id: e.id,
          title: e.title,
          username: e.username,
          url: e.url,
          category: e.category,
        }))
      return JSON.stringify({ count: results.length, results })
    }
    case 'get_password': {
      const title = String(args.title ?? '')
      const entry =
        entries.find((e) => e.title.toLowerCase() === title.toLowerCase()) ||
        entries.find((e) => e.title.toLowerCase().includes(title.toLowerCase()))
      if (!entry) return JSON.stringify({ error: `未找到 ${title}` })
      return JSON.stringify({
        id: entry.id,
        title: entry.title,
        username: entry.username,
        password: entry.password,
        url: entry.url,
        category: entry.category,
        notes: entry.notes,
        custom_fields: entry.customFields.map((f) => ({
          key: f.key,
          value: f.isHidden ? '••••••••' : f.value,
          isHidden: f.isHidden,
        })),
      })
    }
    case 'add_password': {
      const payload = await encryptEntryPayload(key, {
        title: String(args.title ?? ''),
        username: String(args.username ?? ''),
        password: String(args.password ?? ''),
        url: String(args.url ?? ''),
        notes: String(args.notes ?? ''),
        category: String(args.category ?? ''),
        groupId: null,
        customFields: [],
      })
      const created = await api.createEntry(payload)
      return JSON.stringify({ success: true, id: created.id, message: `已添加 ${created.title}` })
    }
    case 'delete_password': {
      const id = String(args.id ?? '')
      await api.deleteEntry(id)
      return JSON.stringify({ success: true, message: '已删除' })
    }
    case 'list_categories': {
      const categories = [...new Set(entries.map((e) => e.category).filter(Boolean))]
      return JSON.stringify({ categories })
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
      return JSON.stringify({ password })
    }
    default:
      return JSON.stringify({ error: `未知工具: ${name}` })
  }
}

function toolLabel(name: string) {
  const map: Record<string, string> = {
    search_passwords: '搜索密码',
    get_password: '获取密码',
    add_password: '添加密码',
    delete_password: '删除密码',
    list_categories: '列出分类',
    generate_password: '生成密码',
  }
  return map[name] ?? name
}

export type { ChatMessage }
