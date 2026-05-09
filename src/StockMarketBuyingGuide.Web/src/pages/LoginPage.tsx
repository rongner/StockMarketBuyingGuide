import { GoogleLogin } from '@react-oauth/google'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { useState } from 'react'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="bg-white rounded-2xl shadow-lg p-10 w-full max-w-sm flex flex-col items-center gap-6">
        <div className="flex flex-col items-center gap-2">
          <div className="w-12 h-12 rounded-full bg-blue-600 flex items-center justify-center">
            <svg className="w-6 h-6 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
            </svg>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">Stock Buying Guide</h1>
          <p className="text-sm text-gray-500 text-center">
            AI-powered daily stock recommendations
          </p>
        </div>

        {error && (
          <div className="w-full rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        )}

        <GoogleLogin
          onSuccess={async ({ credential }) => {
            try {
              setError(null)
              await login(credential!)
              navigate('/', { replace: true })
            } catch {
              setError('Login failed. Please try again.')
            }
          }}
          onError={() => setError('Google sign-in was cancelled or failed.')}
          theme="outline"
          size="large"
          width={340}
        />

        <p className="text-xs text-gray-400 text-center">
          Access restricted to authorised users only.
        </p>
      </div>
    </div>
  )
}
