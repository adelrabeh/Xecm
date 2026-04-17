import React, { useState, useEffect, useRef } from 'react'
import client from '../api/client'

export function UserSearch({ onSelect, selected, placeholder = 'ابحث باسم المستخدم أو الاسم...' }) {
  const [query, setQuery]     = useState('')
  const [results, setResults] = useState([])
  const [loading, setLoading] = useState(false)
  const [open, setOpen]       = useState(false)
  const ref = useRef()

  // Search API
  useEffect(() => {
    if (query.length < 2) { setResults([]); return }
    setLoading(true)
    const timer = setTimeout(async () => {
      try {
        const res = await client.get('/api/v1/workflow/users/search', { params: { q: query } })
        const data = res.data?.data || res.data || []
        setResults(Array.isArray(data) ? data : [])
      } catch {
        // Fallback mock users
        setResults([
          { userId:2, username:'a.zahrani',  fullNameAr:'أحمد الزهراني',  email:'a.zahrani@darah.gov.sa' },
          { userId:3, username:'m.anzi',     fullNameAr:'مريم العنزي',    email:'m.anzi@darah.gov.sa' },
          { userId:4, username:'k.qahtani',  fullNameAr:'خالد القحطاني', email:'k.qahtani@darah.gov.sa' },
          { userId:5, username:'f.shamri',   fullNameAr:'فاطمة الشمري',  email:'f.shamri@darah.gov.sa' },
        ].filter(u =>
          u.username.includes(query) ||
          u.fullNameAr.includes(query) ||
          u.email.includes(query)
        ))
      } finally { setLoading(false) }
    }, 300)
    return () => clearTimeout(timer)
  }, [query])

  // Close on outside click
  useEffect(() => {
    const handler = e => { if (!ref.current?.contains(e.target)) setOpen(false) }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const handleSelect = (user) => {
    onSelect(user)
    setQuery(user.fullNameAr)
    setOpen(false)
  }

  const handleClear = () => { onSelect(null); setQuery(''); setResults([]) }

  return (
    <div ref={ref} className="relative">
      <div className="relative">
        <input
          value={selected ? (selected.fullNameAr || query) : query}
          onChange={e => { setQuery(e.target.value); setOpen(true); if (!e.target.value) onSelect(null) }}
          onFocus={() => setOpen(true)}
          placeholder={placeholder}
          className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right pr-10"
        />
        {selected && (
          <button onClick={handleClear}
            className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-sm">
            ✕
          </button>
        )}
        {loading && (
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-xs animate-spin">⏳</span>
        )}
      </div>

      {/* Selected badge */}
      {selected && (
        <div className="mt-1.5 flex items-center gap-2 bg-blue-50 border border-blue-200 rounded-lg px-2.5 py-1.5">
          <div className="w-6 h-6 bg-blue-600 rounded-full flex items-center justify-center text-white text-[10px] font-bold flex-shrink-0">
            {selected.fullNameAr?.[0] || '؟'}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-blue-800 truncate">{selected.fullNameAr}</p>
            <p className="text-[10px] text-blue-500">{selected.username} • {selected.email}</p>
          </div>
        </div>
      )}

      {/* Dropdown */}
      {open && results.length > 0 && !selected && (
        <div className="absolute z-50 w-full mt-1 bg-white border border-gray-200 rounded-xl shadow-lg overflow-hidden">
          {results.map(user => (
            <button key={user.userId} onClick={() => handleSelect(user)}
              className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-blue-50 transition-colors text-right">
              <div className="w-8 h-8 bg-gray-100 rounded-full flex items-center justify-center text-gray-600 font-bold text-sm flex-shrink-0">
                {user.fullNameAr?.[0] || '؟'}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-gray-800">{user.fullNameAr}</p>
                <p className="text-xs text-gray-400">{user.username} • {user.email}</p>
              </div>
            </button>
          ))}
        </div>
      )}

      {open && query.length >= 2 && results.length === 0 && !loading && (
        <div className="absolute z-50 w-full mt-1 bg-white border border-gray-200 rounded-xl shadow-lg p-3 text-center text-gray-400 text-xs">
          لا توجد نتائج
        </div>
      )}
    </div>
  )
}
