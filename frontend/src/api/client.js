import axios from 'axios'

// Use relative URL so Vercel proxies to Railway (avoids CORS)
// In production: /api/* → https://xecm-production.up.railway.app/api/*
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
    if (err.response?.status === 401) {
      localStorage.removeItem('ecm_token')
      localStorage.removeItem('ecm_user')
      window.location.href = '/login'
    }
    return Promise.reject(err)
  }
)

export default client
