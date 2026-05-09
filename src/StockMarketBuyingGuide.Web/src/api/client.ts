import axios from 'axios'
import { logger } from '@/lib/logger'

const client = axios.create({ baseURL: '/api' })

client.interceptors.request.use(config => {
  const token = localStorage.getItem('smb_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

client.interceptors.response.use(
  res => res,
  error => {
    const status: number | undefined = error.response?.status
    const url: string = error.config?.url ?? 'unknown'
    const method: string = (error.config?.method ?? 'GET').toUpperCase()

    if (status === 401) {
      localStorage.removeItem('smb_token')
      window.location.href = '/login'
    } else if (status === 429) {
      logger.warn(`Rate limited on ${method} ${url}`, 'api')
    } else if (status != null) {
      logger.error(`API ${status} on ${method} ${url}`, 'api', error.response?.data)
    } else {
      logger.error(`Network error on ${method} ${url}`, 'api', error.message)
    }

    return Promise.reject(error)
  }
)

export default client
