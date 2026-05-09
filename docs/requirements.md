# Requirements

## Overview
A personal portfolio project that monitors stock market data and news, then uses Claude AI to recommend 5 stocks to buy each day.

## Tech Stack
- **Frontend:** React
- **Backend:** .NET Web API
- **AI:** Claude API (Anthropic)
- **Market Data:** Free stock data API (e.g., Yahoo Finance, Alpha Vantage)
- **News:** Free news API (e.g., NewsAPI, Finnhub news)

## Features

### 1. Stock Data Monitoring
- Fetch real-time or end-of-day market data for a broad set of stocks
- Data points to include: price, volume, % change, 52-week high/low

### 2. News Monitoring
- Fetch recent financial/market news headlines
- Associate news articles with relevant stock tickers where possible

### 3. AI-Powered Daily Picks
- Send aggregated stock and news data to Claude API
- Claude reasons over the data and returns 5 recommended stocks to buy
- Each recommendation includes:
  - Ticker symbol and company name
  - Reasoning / rationale
  - Key supporting data points

### 4. React Dashboard
- Display the 5 daily stock picks with reasoning
- Show supporting market data and news for each pick
- "Run" button to trigger a fresh recommendation on demand

## Database
- **PostgreSQL** as the primary database
- **Entity Framework Core** for ORM / migrations

### Data to persist

**Recommendation Runs**
- Timestamp of each run
- The 5 stock picks returned by Claude
- Reasoning/rationale per pick
- Price of each pick at time of recommendation

**Stock Snapshots**
- Raw market data fetched each run (ticker, price, volume, % change, etc.)
- Linked to the recommendation run that triggered the fetch

**News Snapshots**
- News headlines and metadata fetched each run
- Linked to the recommendation run

**Performance Tracking**
- Periodically record the current price of previously recommended stocks
- Allow comparison of pick price vs current price to evaluate AI accuracy over time

## Security & Authentication
- Google OAuth 2.0 social login (via a library such as ASP.NET Core Identity or OpenIddict)
- Only authenticated users can access the dashboard and trigger recommendations
- JWT tokens used to secure communication between React frontend and .NET API
- API endpoints protected with `[Authorize]` — no unauthenticated access
- Single-user scope (no user management UI needed, but auth is fully wired for portfolio demonstration)

## Portfolio Simulation
- User configures a starting capital amount (e.g., $1,000) and a date range (e.g., last 1 year)
- Simulation runs as a background job, iterating over each trading day in the range (~252 days/year)
- Each day: capital is split equally across the 5 AI-recommended stocks and "bought" at closing price
- Next trading day: all positions are "sold" at closing price, proceeds reinvested equally into the next 5 picks
- No trading fees or slippage modeled (noted as a simplification)
- News data used where available; stock-data-only for dates beyond NewsAPI's 1-month free tier
- Results saved to the database so the simulation doesn't need to be re-run
- **UI displays:**
  - Starting vs ending portfolio value
  - % gain/loss over the period
  - Day-by-day breakdown (which stocks were held, buy/sell prices, daily P&L)
  - Chart of portfolio value over time

## Backtesting
- User can select a past date in the UI and run the recommendation engine against historical data
- App fetches historical stock market data (OHLCV) for the selected date from a free API
- News used will be pulled from stored snapshots if available, or fetched from NewsAPI for dates within the last month
- Claude receives the historical snapshot and generates 5 picks as if it were that date
- Backtest results are saved to the database alongside regular runs (flagged as backtest)
- **Outcome comparison:** Since the outcome of past dates is known, the UI displays how each backtest pick actually performed (price at recommendation date vs actual price movement in the days/weeks following)

## Out of Scope (for now)
- Automated scheduling (manual trigger only)
- Multi-user support / user management
- Portfolio tracking
- Buy/sell execution
