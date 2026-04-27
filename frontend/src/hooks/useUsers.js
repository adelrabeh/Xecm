/**
 * Single source of truth for users across the entire system.
 * Reads from ecm_users (localStorage) + syncs with API.
 * All pages use this hook — no more hardcoded USERS arrays.
 */
import { useState, useEffect } from 'react'
import client from '../api/client'

const USERS_KEY = 'ecm_users'

const DEFAULT_USERS = [
  { id:1, name:'مدير النظام',    username:'admin',       email:'admin@darah.gov.sa',     dept:'الرئاسة التنفيذية', role:'مدير النظام',  status:'active', roleId:4 },
  { id:2, name:'أحمد الزهراني',  username:'a.zahrani',   email:'a.zahrani@darah.gov.sa', dept:'الشؤون المالية',    role:'مشرف',          status:'active', roleId:2 },
  { id:3, name:'مريم العنزي',    username:'m.anzi',      email:'m.anzi@darah.gov.sa',    dept:'الشؤون الإدارية',  role:'موظف',          status:'active', roleId:1 },
  { id:4, name:'خالد القحطاني', username:'k.qahtani',   email:'k.qahtani@darah.gov.sa', dept:'تقنية المعلومات',  role:'مشرف',          status:'active', roleId:2 },
  { id:5, name:'فاطمة الشمري',  username:'f.shamri',    email:'f.shamri@darah.gov.sa',  dept:'الموارد البشرية',  role:'مدير القسم',   status:'active', roleId:3 },
  { id:6, name:'عمر الدوسري',   username:'o.dosari',    email:'o.dosari@darah.gov.sa',  dept:'التدقيق الداخلي', role:'موظف',          status:'inactive', roleId:1 },
  { id:7, name:'نورة السبيعي',  username:'n.subai',     email:'n.subai@darah.gov.sa',   dept:'إدارة المخاطر',   role:'موظف',          status:'active', roleId:1 },
]

function getStoredUsers() {
  try {
    const stored = JSON.parse(localStorage.getItem(USERS_KEY) || 'null')
    if (Array.isArray(stored) && stored.length > 0) return stored
  } catch {}
  return null
}

function saveUsers(users) {
  try { localStorage.setItem(USERS_KEY, JSON.stringify(users)) } catch {}
}

// Global cache so all hooks share the same data
let _cache = null
let _listeners = []

function notify() { _listeners.forEach(fn => fn([...(_cache || [])])) }

export function useUsers() {
  const [users, setUsersState] = useState(() => {
    if (_cache) return _cache
    const stored = getStoredUsers()
    _cache = stored || DEFAULT_USERS
    if (!stored) saveUsers(_cache)
    return _cache
  })

  useEffect(() => {
    // Register listener
    const fn = (u) => setUsersState(u)
    _listeners.push(fn)

    // Sync with API
    client.get('/api/v1/users')
      .then(r => {
        const data = r.data?.data || r.data
        if (Array.isArray(data) && data.length > 0) {
          const apiUsers = data.map(u => ({
            id:       u.userId || u.id,
            name:     u.fullNameAr || u.username,
            username: u.username,
            email:    u.email,
            dept:     u.jobTitle || u.department || '—',
            role:     u.role || 'موظف',
            roleId:   u.roleId || 1,
            status:   u.isActive ? 'active' : 'inactive',
            fullAccess: u.fullAccess || false,
            password: undefined, // never store from API
          }))
          // Merge: keep local users not in API (local-only accounts)
          const localOnly = (_cache||[]).filter(
            lu => !apiUsers.find(au => au.username === lu.username)
          )
          const merged = [...apiUsers, ...localOnly]
          _cache = merged
          saveUsers(merged)
          notify()
        }
      })
      .catch(() => {}) // silent — use cached

    return () => { _listeners = _listeners.filter(f => f !== fn) }
  }, [])

  // Active users only (for assignment dropdowns)
  const activeUsers = users.filter(u => u.status === 'active')

  // Validate: remove assignee if they no longer exist in users list
  const validateAssignee = (assignedTo) => {
    if (!assignedTo) return null
    return users.find(u => u.id === assignedTo || u.username === assignedTo) || null
  }

  return { users, activeUsers, validateAssignee }
}

// Update from Admin panel
export function updateUsersList(updatedUsers) {
  _cache = updatedUsers
  saveUsers(updatedUsers)
  notify()
}
