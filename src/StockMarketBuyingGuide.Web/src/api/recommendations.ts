import client from './client'

export interface Run {
  id: string
  createdAt: string
  completedAt: string | null
  isBacktest: boolean
  asOfDate: string | null
  errorMessage: string | null
  pickCount: number
}

export interface StockPick {
  id: string
  ticker: string
  companyName: string
  reasoning: string
  priceAtRecommendation: number
}

export interface RunDetail extends Run {
  picks: StockPick[]
  snapshots: {
    ticker: string
    price: number
    volume: number
    pctChange: number
    high52Week: number
    low52Week: number
  }[]
  news: {
    headline: string
    url: string | null
    publishedAt: string
    relatedTickers: string
  }[]
}

export const recommendationsApi = {
  run: () =>
    client.post<{ runId: string }>('/recommendations/run').then(r => r.data),

  list: (limit = 10, offset = 0) =>
    client
      .get<Run[]>('/recommendations', { params: { limit, offset } })
      .then(r => r.data),

  get: (runId: string) =>
    client.get<RunDetail>(`/recommendations/${runId}`).then(r => r.data),
}
