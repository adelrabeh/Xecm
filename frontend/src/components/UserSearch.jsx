import React, { useState, useEffect, useRef } from 'react'
import client from '../api/client'

const MOCK_USERS = [
  { userId:2, username:'a.zahrani',  fullNameAr:'أحمد الزهراني',  email:'a.zahrani@darah.gov.sa' },
  { userId:3, username:'m.anzi',     fullNameAr:'مريم العنزي',    email:'m.anzi@darah.gov.sa' },
  { userId:4, username:'k.qahtani',  fullNameAr:'خالد القحطاني', email:'k.qahtani@darah.gov.sa' },
  { userId:5, username:'f.shamri',   fullNameAr:'فاطمة الشمري',  email:'f.shamri@darah.gov.sa' },
  { userId:6, username:'o.dosari',   fullNameAr:'عمر الدوسري',   email:'o.dosari@darah.gov.sa' },
  { userId:7, username:'n.subai',    fullNameAr:'نورة السبيعي',  email:'n.subai@darah.gov.sa' },
]

export function UserSearch({ onSelect, selected, placeholder = 'ابحث باسم الموظف أو بريده...', exclude = [] }) {
  const [query, setQuery]   = useState('')
  const [results, setResults] = useState([])
  const [loading, setLoading] = useState(false)
  const [open, setOpen]     = useState(false)
  const ref = useRef()
  const inputRef = useRef()

  // Reset input when selected is cleared from outside
  useEffect(() => {
    if (!selected) setQuery('')
  }, [selected])

  useEffect(() => {
    if (query.length < 1) { setResults([]); return }
    setLoading(true)
    const t = setTimeout(async () => {
      try {
        const res = await client.get('/api/v1/workflow/users/search', { params: { q: query } })
        const data = res.data?.data || res.data || []
        const arr = Array.isArray(data) ? data : []
        setResults(arr.filter(u => !exclude.includes(u.userId)))
      } catch {
        setResults(
          MOCK_USERS.filter(u =>
            !exclude.includes(u.userId) &&
            (u.fullNameAr.includes(query) || u.username.includes(query) || u.email.includes(query))
          )
        )
      } finally { setLoading(false) }
    }, 250)
    return () => clearTimeout(t)
  }, [query, exclude.join(',')])

  useEffect(() => {
    const h = e => { if (!ref.current?.contains(e.target)) setOpen(false) }
    document.addEventListener('mousedown', h)
    return () => document.removeEventListener('mousedown', h)
  }, [])

  const handleSelect = (user) => {
    onSelect(user)
    setQuery('')
    setResults([])
    setOpen(false)
  }

  const handleClear = (e) => {
    e.stopPropagation()
    onSelect(null)
    setQuery('')
    setResults([])
    inputRef.current?.focus()
  }

  return (
    <div ref={ref} className="relative">
      <div className="relative">
        <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm pointer-events-none">🔍</span>
        <input
          ref={inputRef}
          value={query}
          onChange={e => { setQuery(e.target.value); setOpen(true) }}
          onFocus={() => { setOpen(true); if (!query && !selected) setResults(MOCK_USERS.filter(u => !exclude.includes(u.userId))) }}
          placeholder={selected ? selected.fullNameAr : placeholder}
          className={`w-full border rounded-xl pl-3 pr-9 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right transition-colors ${
            selected ? 'border-blue-300 bg-blue-50 placeholder-blue-600 font-medium' : 'border-gray-200'
          }`}
        />
        {selected && (
          <button onClick={handleClear}
            className="absolute left-2.5 top-1/2 -translate-y-1/2 w-5 h-5 rounded-full bg-blue-200 hover:bg-red-200 text-blue-700 hover:text-red-700 flex items-center justify-center text-xs transition-colors">
            ✕
          </button>
        )}
        {loading && (
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-xs">⏳</span>
        )}
      </div>

      {/* Selected card */}
      {selected && (
        <div className="mt-1.5 flex items-center gap-2.5 bg-blue-50 border border-blue-200 rounded-xl px-3 py-2">
          <div className="w-7 h-7 bg-blue-600 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
            {selected.fullNameAr?.[0]}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-blue-800">{selected.fullNameAr}</p>
            <p className="text-[10px] text-blue-500">{selected.username} · {selected.email}</p>
          </div>
          <span className="text-green-500 text-sm">✓</span>
        </div>
      )}

      {/* Dropdown */}
      {open && !selected && (
        <div className="absolute z-[100] w-full mt-1 bg-white border border-gray-200 rounded-xl shadow-xl overflow-hidden">
          {results.length > 0
            ? results.map(u => (
                <button key={u.userId} onClick={() => handleSelect(u)}
                  className="w-full flex items-center gap-3 px-4 py-2.5 hover:bg-blue-50 transition-colors text-right border-b border-gray-50 last:border-0">
                  <div className="w-8 h-8 bg-gradient-to-br from-blue-400 to-blue-600 rounded-full flex items-center justify-center text-white font-bold text-sm flex-shrink-0">
                    {u.fullNameAr?.[0]}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-800">{u.fullNameAr}</p>
                    <p className="text-xs text-gray-400">{u.username} · {u.email}</p>
                  </div>
                </button>
              ))
            : query.length >= 1 && !loading && (
                <div className="px-4 py-3 text-center text-gray-400 text-xs">لا توجد نتائج لـ "{query}"</div>
              )
          }
        </div>
      )}
    </div>
  )
}
