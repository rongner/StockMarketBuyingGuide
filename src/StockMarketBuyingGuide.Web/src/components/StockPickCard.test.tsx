import { render, screen } from '@testing-library/react'
import { StockPickCard } from './StockPickCard'
import type { StockPick } from '@/api/recommendations'

const pick: StockPick = {
  id: 'abc-123',
  ticker: 'AAPL',
  companyName: 'Apple Inc.',
  reasoning: 'Strong earnings growth and product pipeline.',
  priceAtRecommendation: 175.5,
}

describe('StockPickCard', () => {
  it('renders ticker and company name', () => {
    render(<StockPickCard pick={pick} rank={1} />)
    expect(screen.getByText('AAPL')).toBeInTheDocument()
    expect(screen.getByText('Apple Inc.')).toBeInTheDocument()
  })

  it('renders the reasoning text', () => {
    render(<StockPickCard pick={pick} rank={1} />)
    expect(screen.getByText('Strong earnings growth and product pipeline.')).toBeInTheDocument()
  })

  it('renders the price formatted to 2 decimals', () => {
    render(<StockPickCard pick={pick} rank={1} />)
    expect(screen.getByText('$175.50')).toBeInTheDocument()
  })

  it('renders the rank number', () => {
    render(<StockPickCard pick={pick} rank={3} />)
    expect(screen.getByText('#3')).toBeInTheDocument()
  })
})
