export type LogLevel = 'debug' | 'info' | 'warn' | 'error'

export interface LogEntry {
  level: LogLevel
  message: string
  context?: string
  data?: unknown
  timestamp: string
}

export interface LogTransport {
  send(entry: LogEntry): void
}

const LEVEL_RANK: Record<LogLevel, number> = { debug: 0, info: 1, warn: 2, error: 3 }

export class ConsoleTransport implements LogTransport {
  send(entry: LogEntry) {
    const prefix = `[${entry.level.toUpperCase()}]${entry.context ? ` [${entry.context}]` : ''}`
    const fn =
      entry.level === 'error' ? console.error :
      entry.level === 'warn'  ? console.warn  :
      entry.level === 'debug' ? console.debug :
      console.info
    if (entry.data !== undefined) {
      fn(prefix, entry.message, entry.data)
    } else {
      fn(prefix, entry.message)
    }
  }
}

// Batches entries and flushes to a backend endpoint on an interval.
// Pass the Authorization header getter so it picks up the current JWT at flush time.
export class ApiTransport implements LogTransport {
  private buffer: LogEntry[] = []
  private timer: ReturnType<typeof setTimeout> | null = null
  private readonly url: string
  private readonly getToken: () => string | null
  private readonly flushMs: number

  constructor(
    url: string,
    getToken: () => string | null = () => localStorage.getItem('smb_token'),
    flushMs = 5_000,
  ) {
    this.url = url
    this.getToken = getToken
    this.flushMs = flushMs
  }

  send(entry: LogEntry) {
    this.buffer.push(entry)
    this.timer ??= setTimeout(() => this.flush(), this.flushMs)
  }

  private async flush() {
    this.timer = null
    if (this.buffer.length === 0) return
    const batch = this.buffer.splice(0)
    try {
      const token = this.getToken()
      await fetch(this.url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify(batch),
      })
    } catch {
      // never throw from a logger
    }
  }
}

class Logger {
  private transports: LogTransport[] = [new ConsoleTransport()]
  private minLevel: LogLevel = 'info'

  configure(options: { transports?: LogTransport[]; minLevel?: LogLevel }) {
    if (options.transports !== undefined) this.transports = options.transports
    if (options.minLevel !== undefined) this.minLevel = options.minLevel
  }

  private emit(level: LogLevel, message: string, context?: string, data?: unknown) {
    if (LEVEL_RANK[level] < LEVEL_RANK[this.minLevel]) return
    const entry: LogEntry = { level, message, context, data, timestamp: new Date().toISOString() }
    for (const t of this.transports) t.send(entry)
  }

  debug(message: string, context?: string, data?: unknown) { this.emit('debug', message, context, data) }
  info (message: string, context?: string, data?: unknown) { this.emit('info',  message, context, data) }
  warn (message: string, context?: string, data?: unknown) { this.emit('warn',  message, context, data) }
  error(message: string, context?: string, data?: unknown) { this.emit('error', message, context, data) }
}

export const logger = new Logger()
