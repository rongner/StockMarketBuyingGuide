import client from './client'

export interface BacktestRun {
  id: string
  createdAt: string
  completedAt: string | null
  asOfDate: string
  errorMessage: string | null
  pickCount: number
}

export interface PerformanceRecord {
  daysAfterPick: number
  currentPrice: number
  pctChange: number
}

export interface BacktestPick {
  id: string
  ticker: string
  companyName: string
  reasoning: string
  priceAtRecommendation: number
  performance: PerformanceRecord[]
}

export interface BacktestDetail {
  id: string
  asOfDate: string
  completedAt: string | null
  errorMessage: string | null
  picks: BacktestPick[]
}

export const backtestApi = {
  run: (date: string) =>
    client.post<{ runId: string }>('/backtest', { date }).then(r => r.data),

  list: (limit = 20, offset = 0) =>
    client
      .get<BacktestRun[]>('/backtest', { params: { limit, offset } })
      .then(r => r.data),

  get: (runId: string) =>
    client.get<BacktestDetail>(`/backtest/${runId}`).then(r => r.data),
}
