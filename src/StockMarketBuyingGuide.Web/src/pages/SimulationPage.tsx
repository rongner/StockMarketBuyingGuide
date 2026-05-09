import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useNavigate } from 'react-router-dom'
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts'
import { useAuth } from '@/auth/AuthContext'
import { simulationApi, type SimulationDetail } from '@/api/simulation'

const MAX_DATE = format(new Date(Date.now() - 86_400_000), 'yyyy-MM-dd')
const DEFAULT_START = '2024-01-01'

export function SimulationPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [startDate, setStartDate] = useState(DEFAULT_START)
  const [endDate, setEndDate] = useState(MAX_DATE)
  const [capital, setCapital] = useState('10000')
  const [activeJobId, setActiveJobId] = useState<string | null>(null)

  // Past jobs list
  const { data: jobs = [] } = useQuery({
    queryKey: ['simulation-jobs'],
    queryFn: () => simulationApi.list(),
  })

  // Active job — polls while running
  const { data: jobDetail } = useQuery({
    queryKey: ['simulation-job', activeJobId],
    queryFn: () => simulationApi.get(activeJobId!),
    enabled: !!activeJobId,
    refetchInterval: query => {
      const s = query.state.data?.status
      return s === 'Running' || s === 'Pending' ? 3000 : false
    },
  })

  // Refresh list when job completes
  useEffect(() => {
    if (jobDetail?.status === 'Completed' || jobDetail?.status === 'Failed') {
      queryClient.invalidateQueries({ queryKey: ['simulation-jobs'] })
    }
  }, [jobDetail?.status, queryClient])

  const startMutation = useMutation({
    mutationFn: () =>
      simulationApi.start(startDate, endDate, parseFloat(capital) || 10_000),
    onSuccess: ({ jobId }) => {
      setActiveJobId(jobId)
      queryClient.invalidateQueries({ queryKey: ['simulation-jobs'] })
    },
  })

  const isRunning = startMutation.isPending

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white border-b border-gray-200 px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center">
              <svg className="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
              </svg>
            </div>
            <span className="font-semibold text-gray-900">Stock Buying Guide</span>
          </div>
          <nav className="flex gap-1">
            <button onClick={() => navigate('/')}
              className="text-sm px-3 py-1.5 rounded-md text-gray-500 hover:text-gray-900 hover:bg-gray-100 transition-colors">
              Dashboard
            </button>
            <button onClick={() => navigate('/backtest')}
              className="text-sm px-3 py-1.5 rounded-md text-gray-500 hover:text-gray-900 hover:bg-gray-100 transition-colors">
              Backtest
            </button>
            <button className="text-sm px-3 py-1.5 rounded-md bg-gray-100 text-gray-900 font-medium">
              Simulation
            </button>
          </nav>
        </div>
        <div className="flex items-center gap-4">
          <span className="text-sm text-gray-500">{user?.email}</span>
          <button onClick={logout}
            className="text-sm text-gray-500 hover:text-gray-900 transition-colors">
            Sign out
          </button>
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex gap-6">
          {/* Main panel */}
          <div className="flex-1 min-w-0">
            {/* Form */}
            <div className="bg-white rounded-xl border border-gray-200 p-5 mb-6">
              <h2 className="text-sm font-semibold text-gray-700 mb-4">New Simulation</h2>
              <div className="flex flex-wrap gap-4 items-end">
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Start date</label>
                  <input type="date" value={startDate} min="2020-01-01" max={MAX_DATE}
                    onChange={e => setStartDate(e.target.value)}
                    className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">End date</label>
                  <input type="date" value={endDate} min="2020-01-02" max={MAX_DATE}
                    onChange={e => setEndDate(e.target.value)}
                    className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Starting capital ($)</label>
                  <input type="number" value={capital} min="100" step="1000"
                    onChange={e => setCapital(e.target.value)}
                    className="border border-gray-300 rounded-lg px-3 py-2 text-sm w-36 focus:outline-none focus:ring-2 focus:ring-blue-500" />
                </div>
                <button
                  onClick={() => startMutation.mutate()}
                  disabled={isRunning || jobDetail?.status === 'Running' || jobDetail?.status === 'Pending'}
                  className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
                >
                  {isRunning ? <><Spinner />Starting…</> : 'Run Simulation'}
                </button>
              </div>
              {startMutation.error && (
                <p className="mt-3 text-sm text-red-600">
                  {(startMutation.error as Error).message}
                </p>
              )}
            </div>

            {/* Results */}
            {jobDetail && <SimulationResults job={jobDetail} />}
          </div>

          {/* Sidebar — past jobs */}
          <aside className="w-64 shrink-0">
            <div className="bg-white rounded-xl border border-gray-200 p-4">
              <h2 className="text-sm font-semibold text-gray-700 mb-3">Past Simulations</h2>
              {jobs.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-4">None yet.</p>
              ) : (
                <ul className="divide-y divide-gray-100">
                  {jobs.map(job => (
                    <li key={job.id}>
                      <button
                        onClick={() => setActiveJobId(job.id)}
                        className={[
                          'w-full text-left px-3 py-2.5 rounded-lg transition-colors',
                          job.id === activeJobId
                            ? 'bg-blue-50 text-blue-700'
                            : 'hover:bg-gray-50 text-gray-700',
                        ].join(' ')}
                      >
                        <div className="text-sm font-medium flex items-center justify-between">
                          <span>
                            {format(new Date(job.startDate), 'MMM yyyy')} →{' '}
                            {format(new Date(job.endDate), 'MMM yyyy')}
                          </span>
                          <StatusDot status={job.status} />
                        </div>
                        <div className="text-xs text-gray-400 mt-0.5">
                          ${job.startingCapital.toLocaleString()}
                          {job.finalCapital
                            ? ` → $${job.finalCapital.toLocaleString(undefined, { maximumFractionDigits: 0 })}`
                            : ` · ${job.progressDays}d done`}
                        </div>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </aside>
        </div>
      </main>
    </div>
  )
}

function SimulationResults({ job }: { job: SimulationDetail }) {
  const isActive = job.status === 'Running' || job.status === 'Pending'
  const totalReturn = job.finalCapital && job.startingCapital
    ? ((job.finalCapital - job.startingCapital) / job.startingCapital * 100)
    : null

  const chartData = job.dayResults.map(d => ({
    date: d.tradingDate,
    capital: parseFloat(d.capitalAfter.toFixed(2)),
    label: format(new Date(d.tradingDate), 'MMM d'),
  }))

  const bestDay = job.dayResults.reduce<typeof job.dayResults[0] | null>(
    (best, d) => (!best || d.dailyReturnPct > best.dailyReturnPct ? d : best), null)
  const worstDay = job.dayResults.reduce<typeof job.dayResults[0] | null>(
    (worst, d) => (!worst || d.dailyReturnPct < worst.dailyReturnPct ? d : worst), null)

  return (
    <div className="space-y-4">
      {/* Progress / stats header */}
      <div className="bg-white rounded-xl border border-gray-200 p-5">
        <div className="flex items-center justify-between mb-3">
          <div>
            <span className="text-sm font-semibold text-gray-800">
              {format(new Date(job.startDate), 'MMM d, yyyy')} →{' '}
              {format(new Date(job.endDate), 'MMM d, yyyy')}
            </span>
            <div className="flex items-center gap-2 mt-1">
              <StatusDot status={job.status} />
              <span className="text-xs text-gray-500 capitalize">{job.status}</span>
              {isActive && (
                <span className="text-xs text-gray-400">
                  {job.progressDays} / {job.totalTradingDays} trading days
                </span>
              )}
            </div>
          </div>
          {totalReturn !== null && (
            <div className="text-right">
              <div className={`text-2xl font-bold ${totalReturn >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                {totalReturn >= 0 ? '+' : ''}{totalReturn.toFixed(2)}%
              </div>
              <div className="text-xs text-gray-400">total return</div>
            </div>
          )}
        </div>

        {isActive && (
          <div className="w-full bg-gray-100 rounded-full h-1.5">
            <div
              className="bg-blue-500 h-1.5 rounded-full transition-all"
              style={{ width: `${job.totalTradingDays > 0 ? (job.progressDays / job.totalTradingDays) * 100 : 0}%` }}
            />
          </div>
        )}

        {job.errorMessage && (
          <p className="mt-3 text-sm text-red-600">{job.errorMessage}</p>
        )}

        {/* Stats row */}
        {job.finalCapital && (
          <div className="grid grid-cols-4 gap-4 mt-4 pt-4 border-t border-gray-100">
            <StatBox label="Starting" value={`$${job.startingCapital.toLocaleString()}`} />
            <StatBox label="Final" value={`$${job.finalCapital.toLocaleString(undefined, { maximumFractionDigits: 0 })}`} />
            <StatBox
              label="Best day"
              value={bestDay ? `+${bestDay.dailyReturnPct.toFixed(2)}%` : '—'}
              positive
            />
            <StatBox
              label="Worst day"
              value={worstDay ? `${worstDay.dailyReturnPct.toFixed(2)}%` : '—'}
              negative={!!worstDay && worstDay.dailyReturnPct < 0}
            />
          </div>
        )}
      </div>

      {/* Chart */}
      {chartData.length > 1 && (
        <div className="bg-white rounded-xl border border-gray-200 p-5">
          <h3 className="text-sm font-semibold text-gray-700 mb-4">Portfolio value</h3>
          <ResponsiveContainer width="100%" height={280}>
            <AreaChart data={chartData} margin={{ top: 4, right: 4, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="capitalGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.15} />
                  <stop offset="95%" stopColor="#3b82f6" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis
                dataKey="label"
                tick={{ fontSize: 11, fill: '#9ca3af' }}
                tickLine={false}
                interval="preserveStartEnd"
              />
              <YAxis
                tick={{ fontSize: 11, fill: '#9ca3af' }}
                tickLine={false}
                axisLine={false}
                tickFormatter={v => `$${(v / 1000).toFixed(0)}k`}
                width={48}
              />
              <Tooltip
                formatter={(v: number) => [`$${v.toLocaleString(undefined, { maximumFractionDigits: 0 })}`, 'Capital']}
                labelFormatter={l => `Date: ${l}`}
                contentStyle={{ fontSize: 12, border: '1px solid #e5e7eb', borderRadius: 8 }}
              />
              <Area
                type="monotone"
                dataKey="capital"
                stroke="#3b82f6"
                strokeWidth={2}
                fill="url(#capitalGrad)"
                dot={false}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  )
}

function StatBox({ label, value, positive, negative }: {
  label: string; value: string; positive?: boolean; negative?: boolean
}) {
  return (
    <div>
      <div className="text-xs text-gray-400">{label}</div>
      <div className={`text-sm font-semibold mt-0.5 ${positive ? 'text-green-600' : negative ? 'text-red-600' : 'text-gray-800'}`}>
        {value}
      </div>
    </div>
  )
}

function StatusDot({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Completed: 'bg-green-500',
    Running: 'bg-yellow-400 animate-pulse',
    Pending: 'bg-yellow-400 animate-pulse',
    Failed: 'bg-red-500',
  }
  return <span className={`inline-block w-2 h-2 rounded-full ${styles[status] ?? 'bg-gray-400'}`} />
}

function Spinner() {
  return (
    <svg className="animate-spin w-4 h-4 text-current" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}
