import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useAuth } from '@/auth/AuthContext'
import { recommendationsApi } from '@/api/recommendations'
import { StockPickCard } from '@/components/StockPickCard'
import { RunsHistory } from '@/components/RunsHistory'

export function DashboardPage() {
  const { user, logout } = useAuth()
  const queryClient = useQueryClient()
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null)

  const { data: runs = [] } = useQuery({
    queryKey: ['runs'],
    queryFn: () => recommendationsApi.list(20),
  })

  const { data: runDetail, isPending: loadingDetail } = useQuery({
    queryKey: ['run', selectedRunId],
    queryFn: () => recommendationsApi.get(selectedRunId!),
    enabled: !!selectedRunId,
  })

  const runMutation = useMutation({
    mutationFn: recommendationsApi.run,
    onSuccess: async ({ runId }) => {
      await queryClient.invalidateQueries({ queryKey: ['runs'] })
      setSelectedRunId(runId)
    },
  })

  // Auto-select the most recent completed run on first load
  useEffect(() => {
    if (!selectedRunId && runs.length > 0) {
      const first = runs.find(r => r.completedAt !== null) ?? runs[0]
      setSelectedRunId(first.id)
    }
  }, [runs, selectedRunId])

  const isRunning = runMutation.isPending
  const runError = runMutation.error as Error | null

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white border-b border-gray-200 px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-blue-600 flex items-center justify-center">
            <svg className="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
            </svg>
          </div>
          <span className="font-semibold text-gray-900">Stock Buying Guide</span>
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
            <div className="flex items-center justify-between mb-6">
              <div>
                <h1 className="text-2xl font-bold text-gray-900">
                  {runDetail?.asOfDate
                    ? `Picks for ${format(new Date(runDetail.asOfDate), 'MMMM d, yyyy')}`
                    : runDetail?.completedAt
                    ? `Picks from ${format(new Date(runDetail.createdAt), 'MMM d, h:mm a')}`
                    : 'Stock Picks'}
                </h1>
                <p className="text-sm text-gray-500 mt-1">AI-generated recommendations</p>
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
                  'Run Now'
                )}
              </button>
            </div>

            {/* Error banner */}
            {runError && (
              <div className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                Run failed: {runError.message}
              </div>
            )}

            {runDetail?.errorMessage && (
              <div className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                This run encountered an error: {runDetail.errorMessage}
              </div>
            )}

            {/* Picks */}
            {loadingDetail ? (
              <div className="rounded-xl border border-gray-200 bg-white p-8 text-center text-gray-400">
                <Spinner className="mx-auto mb-2 w-6 h-6" />
                <p className="text-sm">Loading picks…</p>
              </div>
            ) : runDetail && runDetail.picks.length > 0 ? (
              <div className="flex flex-col gap-3">
                {runDetail.picks.map((pick, i) => (
                  <StockPickCard key={pick.id} pick={pick} rank={i + 1} />
                ))}
              </div>
            ) : selectedRunId ? (
              <div className="rounded-xl border border-gray-200 bg-white p-8 text-center text-gray-400">
                <p className="text-sm">
                  {runDetail
                    ? 'No picks were generated for this run. Make sure your Claude API key is configured.'
                    : 'Select a run from the history to view its picks.'}
                </p>
              </div>
            ) : (
              <div className="rounded-xl border border-gray-200 bg-white p-8 text-center text-gray-400">
                <p className="text-sm">
                  No recommendations yet. Click <strong>Run Now</strong> to generate today's picks.
                </p>
              </div>
            )}
          </div>

          {/* Sidebar */}
          <aside className="w-64 shrink-0">
            <div className="bg-white rounded-xl border border-gray-200 p-4">
              <h2 className="text-sm font-semibold text-gray-700 mb-3">History</h2>
              <RunsHistory
                runs={runs}
                selectedId={selectedRunId}
                onSelect={setSelectedRunId}
              />
            </div>
          </aside>
        </div>
      </main>
    </div>
  )
}

function Spinner({ className = 'w-4 h-4' }: { className?: string }) {
  return (
    <svg
      className={`animate-spin ${className} text-current`}
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
      />
    </svg>
  )
}
