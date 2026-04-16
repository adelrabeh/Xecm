import axios from 'axios'

const client = axios.create({
  baseURL: '',
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' }
})

client.interceptors.request.use(config => {
  const token = localStorage.getItem('ecm_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

client.interceptors.response.use(
  res => res,
  err => {
    // Only force logout if auth/me fails (token truly invalid)
    // Do NOT logout on 401 from other endpoints - just let the page handle it
    const url = err.config?.url || ''
    if (err.response?.status === 401 && url.includes('/auth/me')) {
      localStorage.removeItem('ecm_token')
      localStorage.removeItem('ecm_user')
      window.location.href = '/login'
    }
    return Promise.reject(err)
  }
)

export default client
