import { create } from 'zustand'
import client from '../api/client'

// ─── Local user lookup — checks ecm_users localStorage ───────────────────────
function findLocalUser(username, password) {
  try {
    const stored = JSON.parse(localStorage.getItem('ecm_users') || '[]')
    const users  = Array.isArray(stored) ? stored : []
    const user   = users.find(u =>
      u.username?.toLowerCase() === username?.toLowerCase() &&
      u.password === password &&
      u.status !== 'inactive'
    )
    if (!user) return null
    return {
      userId:     user.id,
      username:   user.username,
      fullNameAr: user.name,
      fullNameEn: user.name,
      email:      user.email,
      language:   'ar',
      dept:       user.dept,
      permissions: user.fullAccess
        ? ['admin.*','documents.*','workflow.*','audit.*','records.*']
        : ['documents.read','workflow.read'],
    }
  } catch { return null }
}

// Generate a local JWT-like token for non-API users
function makeLocalToken(user) {
  const payload = btoa(JSON.stringify({ uid: user.userId, username: user.username, exp: Date.now() + 8*3600*1000 }))
  return `local.${payload}.sig`
}

export const useAuthStore = create((set, get) => ({
  user:      JSON.parse(localStorage.getItem('ecm_user')  || 'null'),
  token:     localStorage.getItem('ecm_token'),
  isLoading: false,
  error:     null,

  login: async (username, password) => {
    set({ isLoading: true, error: null })

    // ── Try API first ─────────────────────────────────────────────────────────
    try {
      const res  = await client.post('/api/v1/auth/login', { username, password })
      const data = res.data?.data || res.data
      const token = data?.token
      if (!token) throw new Error('no token')

      const user = {
        userId:     data.userId,
        username:   data.username,
        fullNameAr: data.fullNameAr,
        fullNameEn: data.fullNameEn,
        email:      data.email,
        language:   data.language,
        permissions: data.permissions || [],
      }
      localStorage.setItem('ecm_token', token)
      localStorage.setItem('ecm_user',  JSON.stringify(user))
      set({ user, token, isLoading: false })
      return true
    } catch {}

    // ── Fallback: check local users created in Admin panel ────────────────────
    const localUser = findLocalUser(username, password)
    if (localUser) {
      const token = makeLocalToken(localUser)
      localStorage.setItem('ecm_token', token)
      localStorage.setItem('ecm_user',  JSON.stringify(localUser))
      set({ user: localUser, token, isLoading: false })
      return true
    }

    // ── Both failed ───────────────────────────────────────────────────────────
    const msg = 'اسم المستخدم أو كلمة المرور غير صحيحة'
    set({ error: msg, isLoading: false })
    return false
  },

  logout: () => {
    localStorage.removeItem('ecm_token')
    localStorage.removeItem('ecm_user')
    set({ user: null, token: null })
  },

  isAuthenticated: () => !!get().token,
}))
