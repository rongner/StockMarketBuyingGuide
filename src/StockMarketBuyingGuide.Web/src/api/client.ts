import axios from 'axios'

const client = axios.create({ baseURL: '/api' })

client.interceptors.request.use(config => {
  const token = localStorage.getItem('smb_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

client.interceptors.response.use(
  res => res,
  error => {
    if (error.response?.status === 401) {
      localStorage.removeItem('smb_token')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

export default client
