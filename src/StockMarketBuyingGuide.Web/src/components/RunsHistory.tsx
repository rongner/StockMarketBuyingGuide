import { format } from 'date-fns'
import type { Run } from '@/api/recommendations'

interface Props {
  runs: Run[]
  selectedId: string | null
  onSelect: (id: string) => void
}

export function RunsHistory({ runs, selectedId, onSelect }: Props) {
  if (runs.length === 0) {
    return (
      <div className="text-sm text-gray-400 text-center py-6">
        No past runs yet.
      </div>
    )
  }

  return (
    <ul className="divide-y divide-gray-100">
      {runs.map(run => {
        const isSelected = run.id === selectedId
        const date = run.asOfDate
          ? format(new Date(run.asOfDate), 'MMM d, yyyy')
          : format(new Date(run.createdAt), 'MMM d, h:mm a')
        const status =
          run.errorMessage
            ? 'error'
            : run.completedAt
            ? 'done'
            : 'running'

        return (
          <li key={run.id}>
            <button
              onClick={() => onSelect(run.id)}
              className={[
                'w-full text-left px-3 py-2.5 rounded-lg transition-colors',
                isSelected
                  ? 'bg-blue-50 text-blue-700'
                  : 'hover:bg-gray-50 text-gray-700',
              ].join(' ')}
            >
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">{date}</span>
                <StatusDot status={status} />
              </div>
              <div className="text-xs text-gray-400 mt-0.5">
                {run.isBacktest ? 'Backtest' : 'Live'} · {run.pickCount} picks
              </div>
            </button>
          </li>
        )
      })}
    </ul>
  )
}

function StatusDot({ status }: { status: 'done' | 'running' | 'error' }) {
  const styles = {
    done: 'bg-green-500',
    running: 'bg-yellow-400 animate-pulse',
    error: 'bg-red-500',
  }
  return (
    <span className={`inline-block w-2 h-2 rounded-full ${styles[status]}`} />
  )
}
