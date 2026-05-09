import type { StockPick } from '@/api/recommendations'

interface Props {
  pick: StockPick
  rank: number
}

export function StockPickCard({ pick, rank }: Props) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 flex flex-col gap-3 hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <span className="text-xs font-medium text-gray-400 w-5">#{rank}</span>
          <div>
            <span className="text-lg font-bold text-blue-600">{pick.ticker}</span>
            <p className="text-xs text-gray-500 mt-0.5">{pick.companyName}</p>
          </div>
        </div>
        <div className="text-right">
          <span className="text-base font-semibold text-gray-900">
            ${pick.priceAtRecommendation.toFixed(2)}
          </span>
          <p className="text-xs text-gray-400 mt-0.5">at recommendation</p>
        </div>
      </div>
      <p className="text-sm text-gray-600 leading-relaxed border-t border-gray-100 pt-3">
        {pick.reasoning}
      </p>
    </div>
  )
}
