import client from './client'

export interface SimulationJob {
  id: string
  createdAt: string
  status: 'Pending' | 'Running' | 'Completed' | 'Failed'
  startDate: string
  endDate: string
  startingCapital: number
  finalCapital: number | null
  errorMessage: string | null
  progressDays: number
}

export interface DayResult {
  tradingDate: string
  capitalBefore: number
  capitalAfter: number
  dailyReturnPct: number
}

export interface SimulationDetail extends SimulationJob {
  totalTradingDays: number
  dayResults: DayResult[]
}

export const simulationApi = {
  start: (startDate: string, endDate: string, startingCapital: number) =>
    client
      .post<{ jobId: string; cached: boolean }>('/simulation', {
        startDate,
        endDate,
        startingCapital,
      })
      .then(r => r.data),

  get: (jobId: string) =>
    client.get<SimulationDetail>(`/simulation/${jobId}`).then(r => r.data),

  list: (limit = 10) =>
    client.get<SimulationJob[]>('/simulation', { params: { limit } }).then(r => r.data),
}
