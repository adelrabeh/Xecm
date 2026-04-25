import axios from 'axios'

const client = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'https://xecm-production.up.railway.app',
  timeout: 15000,
})

// Request: inject JWT + Accept-Language
client.interceptors.request.use(config => {
  const token = localStorage.getItem('ecm_token')
  const lang  = localStorage.getItem('ecm_lang') || 'ar'
  if (token) config.headers['Authorization'] = `Bearer ${token}`
  config.headers['Accept-Language'] = lang
  config.headers['X-Lang']          = lang
  return config
})

// Response: handle 401
client.interceptors.response.use(
  r => r,
  err => {
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
