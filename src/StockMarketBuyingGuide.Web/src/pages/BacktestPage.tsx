import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { backtestApi, type BacktestPick } from '@/api/backtest'

const MAX_DATE = format(new Date(Date.now() - 86_400_000), 'yyyy-MM-dd') // yesterday
const MIN_DATE = '2020-01-01'

export function BacktestPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [selectedDate, setSelectedDate] = useState(MAX_DATE)
  const [preferredRunId, setSelectedRunId] = useState<string | null>(null)

  const { data: runs = [] } = useQuery({
    queryKey: ['backtest-runs'],
    queryFn: () => backtestApi.list(),
  })

  const { data: runDetail, isPending: loadingDetail } = useQuery({
    queryKey: ['backtest-run', selectedRunId],
    queryFn: () => backtestApi.get(selectedRunId!),
    enabled: !!selectedRunId,
  })

  const runMutation = useMutation({
    mutationFn: () => backtestApi.run(selectedDate),
    onSuccess: async ({ runId }) => {
      await queryClient.invalidateQueries({ queryKey: ['backtest-runs'] })
      setSelectedRunId(runId)
    },
  })

  const selectedRunId = preferredRunId ?? runs[0]?.id ?? null

  const isRunning = runMutation.isPending

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
            <button
              onClick={() => navigate('/')}
              className="text-sm px-3 py-1.5 rounded-md text-gray-500 hover:text-gray-900 hover:bg-gray-100 transition-colors"
            >
              Dashboard
            </button>
            <button
              className="text-sm px-3 py-1.5 rounded-md bg-gray-100 text-gray-900 font-medium"
            >
              Backtest
            </button>
            <button
              onClick={() => navigate('/simulation')}
              className="text-sm px-3 py-1.5 rounded-md text-gray-500 hover:text-gray-900 hover:bg-gray-100 transition-colors"
            >
              Simulation
            </button>
          </nav>
        </div>
        <div className="flex items-center gap-4">
          <span className="text-sm text-gray-500">{user?.email}</span>
          <button
            onClick={logout}
            className="text-sm text-gray-500 hover:text-gray-900 transition-colors"
          >
            Sign out
          </button>
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex gap-6">
          {/* Main panel */}
          <div className="flex-1 min-w-0">
            {/* Action bar */}
            <div className="flex items-end gap-3 mb-6">
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Backtest date</label>
                <input
                  type="date"
                  value={selectedDate}
                  min={MIN_DATE}
                  max={MAX_DATE}
                  onChange={e => setSelectedDate(e.target.value)}
                  className="border border-gray-300 rounded-lg px-3 py-2 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <button
                onClick={() => runMutation.mutate()}
                disabled={isRunning}
                className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
              >
                {isRunning ? (
                  <>
                    <Spinner />
                    Running…
                  </>
                ) : (
                  'Run Backtest'
                )}
              </button>
            </div>

            {/* Error banner */}
            {runMutation.error && (
              <div className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                Run failed: {(runMutation.error as Error).message}
              </div>
            )}

            {/* Results */}
            {loadingDetail ? (
              <div className="rounded-xl border border-gray-200 bg-white p-8 text-center text-gray-400">
                <Spinner className="mx-auto mb-2 w-6 h-6" />
                <p className="text-sm">Loading results…</p>
              </div>
            ) : runDetail ? (
              <>
                <div className="mb-3 text-sm text-gray-500">
                  Backtest for{' '}
                  <span className="font-medium text-gray-800">
                    {format(new Date(runDetail.asOfDate), 'MMMM d, yyyy')}
                  </span>
                </div>
                {runDetail.errorMessage && (
                  <div className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                    {runDetail.errorMessage}
                  </div>
                )}
                {runDetail.picks.length > 0 ? (
                  <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-gray-100 text-xs text-gray-500 uppercase tracking-wide">
                          <th className="text-left px-4 py-3 font-medium">Ticker</th>
                          <th className="text-right px-4 py-3 font-medium">At rec.</th>
                          <th className="text-right px-4 py-3 font-medium">+1d</th>
                          <th className="text-right px-4 py-3 font-medium">+5d</th>
                          <th className="text-right px-4 py-3 font-medium">+20d</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-50">
                        {runDetail.picks.map(pick => (
                          <BacktestRow key={pick.id} pick={pick} />
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <EmptyState message="No picks were generated for this run." />
                )}
              </>
            ) : (
              <EmptyState message="Select a backtest from the history or run a new one." />
            )}
          </div>

          {/* Sidebar */}
          <aside className="w-64 shrink-0">
            <div className="bg-white rounded-xl border border-gray-200 p-4">
              <h2 className="text-sm font-semibold text-gray-700 mb-3">Past Backtests</h2>
              {runs.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-4">No backtests yet.</p>
              ) : (
                <ul className="divide-y divide-gray-100">
                  {runs.map(run => (
                    <li key={run.id}>
                      <button
                        onClick={() => setSelectedRunId(run.id)}
                        className={[
                          'w-full text-left px-3 py-2.5 rounded-lg transition-colors',
                          run.id === selectedRunId
                            ? 'bg-blue-50 text-blue-700'
                            : 'hover:bg-gray-50 text-gray-700',
                        ].join(' ')}
                      >
                        <div className="text-sm font-medium">
                          {format(new Date(run.asOfDate), 'MMM d, yyyy')}
                        </div>
                        <div className="text-xs text-gray-400 mt-0.5">
                          {run.pickCount} picks
                          {run.errorMessage ? ' · error' : ''}
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

function BacktestRow({ pick }: { pick: BacktestPick }) {
  const perf = Object.fromEntries(pick.performance.map(p => [p.daysAfterPick, p]))

  return (
    <tr className="hover:bg-gray-50">
      <td className="px-4 py-3">
        <div className="font-semibold text-blue-600">{pick.ticker}</div>
        <div className="text-xs text-gray-400">{pick.companyName}</div>
      </td>
      <td className="px-4 py-3 text-right text-gray-700">
        ${pick.priceAtRecommendation.toFixed(2)}
      </td>
      {[1, 5, 20].map(days => {
        const rec = perf[days]
        return (
          <td key={days} className="px-4 py-3 text-right">
            {rec ? (
              <>
                <div className="text-gray-700">${rec.currentPrice.toFixed(2)}</div>
                <div className={rec.pctChange >= 0 ? 'text-green-600 text-xs' : 'text-red-600 text-xs'}>
                  {rec.pctChange >= 0 ? '+' : ''}{rec.pctChange.toFixed(2)}%
                </div>
              </>
            ) : (
              <span className="text-gray-300">—</span>
            )}
          </td>
        )
      })}
    </tr>
  )
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-8 text-center text-gray-400">
      <p className="text-sm">{message}</p>
    </div>
  )
}

function Spinner({ className = 'w-4 h-4' }: { className?: string }) {
  return (
    <svg className={`animate-spin ${className} text-current`} fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  )
}
