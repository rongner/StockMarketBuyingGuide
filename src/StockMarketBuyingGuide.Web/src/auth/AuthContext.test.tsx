import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { AuthProvider, useAuth } from './AuthContext'

vi.mock('@/api/client', () => ({
  default: {
    post: vi.fn().mockResolvedValue({
      data: { token: 'test-token', email: 'test@example.com' }
    }),
    interceptors: {
      request: { use: vi.fn() },
      response: { use: vi.fn() },
    },
  },
}))

function TestComponent() {
  const { user, login, logout } = useAuth()
  return (
    <div>
      <span data-testid="email">{user?.email ?? 'none'}</span>
      <button onClick={() => login('google-credential')}>Login</button>
      <button onClick={logout}>Logout</button>
    </div>
  )
}

function renderWithAuth() {
  return render(
    <AuthProvider>
      <TestComponent />
    </AuthProvider>
  )
}

beforeEach(() => {
  localStorage.clear()
})

describe('AuthContext', () => {
  it('starts with no user when localStorage is empty', () => {
    renderWithAuth()
    expect(screen.getByTestId('email').textContent).toBe('none')
  })

  it('login stores token and email in localStorage', async () => {
    renderWithAuth()
    await userEvent.click(screen.getByText('Login'))
    expect(localStorage.getItem('smb_token')).toBe('test-token')
    expect(localStorage.getItem('smb_email')).toBe('test@example.com')
  })

  it('login sets user in context', async () => {
    renderWithAuth()
    await userEvent.click(screen.getByText('Login'))
    expect(screen.getByTestId('email').textContent).toBe('test@example.com')
  })

  it('logout clears token and email from localStorage', async () => {
    renderWithAuth()
    await userEvent.click(screen.getByText('Login'))
    await userEvent.click(screen.getByText('Logout'))
    expect(localStorage.getItem('smb_token')).toBeNull()
    expect(localStorage.getItem('smb_email')).toBeNull()
  })

  it('logout clears user from context', async () => {
    renderWithAuth()
    await userEvent.click(screen.getByText('Login'))
    await userEvent.click(screen.getByText('Logout'))
    expect(screen.getByTestId('email').textContent).toBe('none')
  })

  it('restores user from localStorage on mount', () => {
    localStorage.setItem('smb_token', 'saved-token')
    localStorage.setItem('smb_email', 'saved@example.com')
    renderWithAuth()
    expect(screen.getByTestId('email').textContent).toBe('saved@example.com')
  })
})
