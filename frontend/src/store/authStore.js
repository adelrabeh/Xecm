import { create } from 'zustand'
import client from '../api/client'

export const useAuthStore = create((set, get) => ({
  user: JSON.parse(localStorage.getItem('ecm_user') || 'null'),
  token: localStorage.getItem('ecm_token'),
  isLoading: false,
  error: null,

  login: async (username, password) => {
    set({ isLoading: true, error: null })
    try {
      const res = await client.post('/api/v1/auth/login', { username, password })
      const { token, user } = res.data.data || res.data
      localStorage.setItem('ecm_token', token)
      localStorage.setItem('ecm_user', JSON.stringify(user))
      set({ user, token, isLoading: false })
      return true
    } catch (err) {
      const msg = err.response?.data?.message || 'فشل تسجيل الدخول'
      set({ error: msg, isLoading: false })
      return false
    }
  },

  logout: () => {
    localStorage.removeItem('ecm_token')
    localStorage.removeItem('ecm_user')
    set({ user: null, token: null })
  },

  isAuthenticated: () => !!get().token,
}))
