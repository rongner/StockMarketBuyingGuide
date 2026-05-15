import { createContext, useContext, useState, useCallback, type ReactNode } from 'react'
import client from '@/api/client'

interface AuthUser {
  email: string
  token: string
}

interface AuthContextValue {
  user: AuthUser | null
  login: (credential: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = localStorage.getItem('smb_token')
    const email = localStorage.getItem('smb_email')
    return token && email ? { token, email } : null
  })

  const login = useCallback(async (credential: string) => {
    const { data } = await client.post<{ token: string; email: string }>(
      '/auth/google-login',
      { credential }
    )
    localStorage.setItem('smb_token', data.token)
    localStorage.setItem('smb_email', data.email)
    setUser({ token: data.token, email: data.email })
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem('smb_token')
    localStorage.removeItem('smb_email')
    setUser(null)
  }, [])

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
