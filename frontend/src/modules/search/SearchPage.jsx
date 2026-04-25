import React, { useState, useEffect, useRef, useCallback } from 'react'
import { useLang } from '../../i18n.js'
import { useNavigate } from 'react-router-dom'
import { useLocalStorage } from '../../hooks/useLocalStorage'
import { useToast } from '../../components/Toast'
import client from '../../api/client'

const TYPE_CFG = {
  'وثيقة':{'icon':'📄','color':'#2563eb','bg':'#eff6ff','border':'#bfdbfe'},
  'سجل':{'icon':'🗂','color':'#7c3aed','bg':'#f5f3ff','border':'#ddd6fe'},
  'أصل معرفي':{'icon':'📦','color':'#059669','bg':'#ecfdf5','border':'#a7f3d0'},
  'Document':{'icon':'📄','color':'#2563eb','bg':'#eff6ff','border':'#bfdbfe'},
  'Record':{'icon':'🗂','color':'#7c3aed','bg':'#f5f3ff','border':'#ddd6fe'},
  'Knowledge Asset':{'icon':'📦','color':'#059669','bg':'#ecfdf5','border':'#a7f3d0'},
}

// ─── Build mock search index from localStorage ─────────────────────────────
function buildLocalIndex() {
  const results = []
  try {
    // Documents
    const docs = JSON.parse(localStorage.getItem('ecm_docs') || '[]')
    const libFiles = JSON.parse(localStorage.getItem('ecm_library_files_v2') || '[]')
    ;[...docs, ...libFiles].forEach(d => {
      if (!d?.titleAr && !d?.name) return
      results.push({
        id: d.id || d.documentId,
        type: 'doc', typeAr: 'وثيقة', typeIcon: '📄',
        title: d.titleAr || d.name || d.title,
        titleEn: d.titleEn,
        summary: d.summary || d.description || '',
        status: d.status,
        date: d.createdAt || d.created,
        tags: typeof d.tags === 'string' ? d.tags : (d.tags||[]).join('، '),
        url: '/documents',
        highlights: null,
        score: 0,
        fileType: d.fileType || d.type,
        classification: d.classification,
        owner: d.owner,
      })
    })
    // Records
    const records = JSON.parse(localStorage.getItem('ecm_records_v2') || '[]')
    records.forEach(r => {
      if (!r?.titleAr) return
      results.push({
        id: r.id, type: 'record', typeAr: 'سجل', typeIcon: '🗂',
        title: r.titleAr, titleEn: r.titleEn,
        summary: r.desc || '',
        status: r.status, date: r.created,
        tags: (r.tags||[]).join('، '),
        url: '/records', highlights: null, score: 0,
        dept: r.dept,
      })
    })
  } catch {}
  return results
}

function scoreAndHighlight(item, terms) {
  let score = 0
  const hl = []
  const text = (item.title + ' ' + item.summary + ' ' + item.tags).toLowerCase()
  terms.forEach(t => {
    const tl = t.toLowerCase()
    if (item.title?.toLowerCase().includes(tl))   score += 3
    if (item.tags?.toLowerCase().includes(tl))    score += 2
    if (item.summary?.toLowerCase().includes(tl)) score += 1
    if (text.includes(tl)) hl.push(t)
  })
  // Build highlight snippet
  let snippet = item.summary || item.title || ''
  hl.forEach(t => {
    snippet = snippet.replace(new RegExp(t, 'gi'), m => `~~${m}~~`)
  })
  return { ...item, score, highlights: snippet.length > 120 ? snippet.slice(0,120)+'...' : snippet }
}

// ─── Highlight renderer ─────────────────────────────────────────────────────
function Highlight({ text }) {
  if (!text) return null
  const parts = text.split(/(~~[^~]+~~)/)
  return (
    <span>
      {parts.map((p, i) =>
        p.startsWith('~~') && p.endsWith('~~')
          ? <mark key={i} style={{background:'#fef08a', borderRadius:2, padding:'0 2px'}}>{p.slice(2,-2)}</mark>
          : p
      )}
    </span>
  )
}

// ─── Result card ────────────────────────────────────────────────────────────
function ResultCard({ item, query, onClick }) {
  const cfg = TYPE_CFG[item.typeAr] || TYPE_CFG['وثيقة']
  return (
    <div onClick={onClick}
      className="bg-white rounded-2xl border hover:shadow-md transition-all cursor-pointer p-4 group"
      style={{borderColor: cfg.border}}>
      <div className="flex items-start gap-3">
        <div className="w-10 h-10 rounded-xl flex items-center justify-center text-xl flex-shrink-0"
          style={{background: cfg.bg}}>
          {item.typeIcon}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2 mb-1">
            <p className="font-bold text-gray-900 text-sm leading-snug group-hover:text-blue-700 transition-colors">
              {item.title}
            </p>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full flex-shrink-0"
              style={{background:cfg.bg, color:cfg.color}}>
              {item.typeAr}
            </span>
          </div>
          {item.highlights && (
            <p className="text-xs text-gray-500 leading-relaxed mt-1">
              <Highlight text={item.highlights}/>
            </p>
          )}
          <div className="flex items-center gap-3 mt-2 text-[10px] text-gray-400 flex-wrap">
            {item.status   && <span>● {item.status}</span>}
            {item.owner    && <span>👤 {item.owner}</span>}
            {item.dept     && <span>🏛 {item.dept}</span>}
            {item.fileType && <span className="font-mono">{item.fileType}</span>}
            {item.classification && <span style={{color:cfg.color, fontWeight:600}}>{item.classification}</span>}
            {item.date && <span>📅 {item.date?.split('T')[0]}</span>}
            {item.score > 0 && (
              <span className="mr-auto text-blue-400 font-semibold">
                {'★'.repeat(Math.min(3, Math.round(item.score/2)))} صلة: {item.score.toFixed(1)}
              </span>
            )}
          </div>
          {item.tags && (
            <div className="flex flex-wrap gap-1 mt-2">
              {item.tags.split('،').filter(Boolean).slice(0,4).map(t => (
                <span key={t} className="text-[9px] bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded-full">#{t.trim()}</span>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// ─── Main Search Page ────────────────────────────────────────────────────────
export default function SearchPage() {
  const { lang, setLang, t, isRTL, fmtDate, fmtNum } = useLang()
  const navigate = useNavigate()
  const { show, ToastContainer } = useToast()
  const [query, setQuery]         = useState('')
  const [results, setResults]     = useState([])
  const [loading, setLoading]     = useState(false)
  const [searched, setSearched]   = useState(false)
  const [filterType, setFT]       = useState('all')
  const [suggestions, setSugg]    = useState([])
  const [showSugg, setShowSugg]   = useState(false)
  const [history, setHistory]     = useLocalStorage('ecm_search_history', [])
  const [total, setTotal]         = useState(0)
  const [facets, setFacets]       = useState(null)
  const inputRef = useRef()
  const debounceRef = useRef()

  // Autofocus
  useEffect(() => { inputRef.current?.focus() }, [])

  const localIndex = React.useMemo(() => buildLocalIndex(), [])

  const doSearch = useCallback(async (q) => {
    if (!q?.trim()) return
    setLoading(true); setSearched(true); setShowSugg(false)

    // Save to history
    setHistory(prev => {
      const h = [q, ...(Array.isArray(prev)?prev:[]).filter(x=>x!==q)].slice(0,8)
      return h
    })

    // Search local index
    const terms = q.trim().split(/\s+/)
    let localResults = localIndex
      .map(item => scoreAndHighlight(item, terms))
      .filter(item => item.score > 0)
      .sort((a,b) => b.score - a.score)

    // Try API
    try {
      const res = await client.get('/api/v1/search', { params: { q, pageSize: 50 } })
      const data = res.data?.data
      if (data?.results?.length > 0) {
        // Merge API + local, dedup by id
        const apiIds = new Set(data.results.map(r=>r.id))
        const merged = [
          ...data.results,
          ...localResults.filter(r => !apiIds.has(r.id))
        ]
        setResults(merged)
        setTotal(data.total + localResults.length)
        setFacets(data.facets)
        setLoading(false)
        return
      }
    } catch {}

    // Use local only
    const facetTypes = {}
    localResults.forEach(r => { facetTypes[r.typeAr] = (facetTypes[r.typeAr]||0)+1 })
    setResults(localResults)
    setTotal(localResults.length)
    setFacets({ byType: Object.entries(facetTypes).map(([label,count])=>({label,icon:TYPE_CFG[label]?.icon,count})) })
    setLoading(false)
  }, [localIndex])

  // Suggest
  const doSuggest = useCallback(async (q) => {
    if (q.length < 2) { setSugg([]); return }
    const local = localIndex
      .filter(item => item.title?.includes(q))
      .map(item => item.title).slice(0,6)
    setSugg([...new Set(local)])
    try {
      const res = await client.get('/api/v1/search/suggest', { params:{q} })
      const api = res.data?.data || []
      setSugg(prev => [...new Set([...prev, ...api])].slice(0,8))
    } catch {}
  }, [localIndex])

  const handleInput = (val) => {
    setQuery(val)
    clearTimeout(debounceRef.current)
    if (val.length >= 2) {
      setShowSugg(true)
      debounceRef.current = setTimeout(() => doSuggest(val), 200)
    } else {
      setSugg([]); setShowSugg(false)
    }
  }

  const handleSearch = () => { if (query.trim()) doSearch(query) }

  const filtered = filterType === 'all' ? results : results.filter(r => r.typeAr === filterType ||
    (filterType === 'doc' && r.typeAr === 'وثيقة') ||
    (filterType === 'record' && r.typeAr === 'سجل') ||
    (filterType === 'asset' && r.typeAr === 'أصل معرفي'))

  const QUICK_SEARCHES = ['عقود','تقارير','سياسات','محاضر','ميزانية','مخطوطات','أبحاث']

  return (
    <div className="max-w-3xl mx-auto" style={{paddingBottom:80}}>
      <ToastContainer/>

      {/* ── Search bar ── */}
      <div className="mb-6">
        <h1 className="text-xl font-black text-gray-900 mb-4">t('search_title')</h1>
        <div className="relative">
          <div className="flex gap-2">
            <div className="flex-1 relative">
              <input
                ref={inputRef}
                value={query}
                onChange={e => handleInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleSearch()}
                onFocus={() => query.length >= 2 && setShowSugg(true)}
                onBlur={() => setTimeout(() => setShowSugg(false), 150)}
                placeholder={t("search_ph")}
                className="w-full border-2 border-gray-200 focus:border-blue-500 rounded-2xl px-5 py-4 text-base focus:outline-none text-right shadow-sm transition-colors"
                style={{fontSize:15}}
              />
              {query && (
                <button onClick={()=>{setQuery('');setResults([]);setSearched(false)}}
                  className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-xl">
                  ✕
                </button>
              )}

              {/* Suggestions */}
              {showSugg && suggestions.length > 0 && (
                <div className="absolute top-full right-0 left-0 mt-1 bg-white border border-gray-200 rounded-xl shadow-xl z-50 overflow-hidden">
                  {suggestions.map(s => (
                    <button key={s} onClick={() => { setQuery(s); doSearch(s) }}
                      className="w-full text-right px-4 py-2.5 text-sm hover:bg-blue-50 transition-colors flex items-center gap-2 text-gray-700 border-b border-gray-50 last:border-0">
                      <span className="text-gray-400">🔍</span>
                      <span dangerouslySetInnerHTML={{__html: s.replace(new RegExp(query,'gi'), m=>`<strong style="color:#2563eb">${m}</strong>`)}}/>
                    </button>
                  ))}
                </div>
              )}
            </div>
            <button onClick={handleSearch} disabled={!query.trim() || loading}
              className="px-6 py-4 bg-blue-700 text-white rounded-2xl font-bold hover:bg-blue-800 disabled:opacity-40 transition-colors flex items-center gap-2 shadow-sm whitespace-nowrap">
              {loading ? '⏳' : '🔍'} بحث
            </button>
          </div>
        </div>

        {/* Quick searches */}
        {!searched && (
          <div className="flex flex-wrap gap-2 mt-3">
            {QUICK_SEARCHES.map(q => (
              <button key={q} onClick={() => { setQuery(q); doSearch(q) }}
                className="text-xs border border-gray-200 text-gray-600 hover:border-blue-400 hover:text-blue-600 hover:bg-blue-50 px-3 py-1.5 rounded-full transition-all font-medium">
                {q}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* ── Search history ── */}
      {!searched && Array.isArray(history) && history.length > 0 && (
        <div className="mb-6">
          <div className="flex items-center justify-between mb-2">
            <p className="text-xs font-black text-gray-400 uppercase">t('search_history')</p>
            <button onClick={() => setHistory([])} className="text-xs text-red-400 hover:text-red-600">t('clear_history')</button>
          </div>
          <div className="flex flex-wrap gap-2">
            {history.map(h => (
              <button key={h} onClick={() => { setQuery(h); doSearch(h) }}
                className="flex items-center gap-1.5 text-xs bg-gray-100 hover:bg-gray-200 text-gray-600 px-3 py-1.5 rounded-full transition-colors">
                🕐 {h}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* ── Results ── */}
      {searched && !loading && (
        <>
          {/* Summary + facets */}
          <div className="flex items-center justify-between mb-3 flex-wrap gap-2">
            <p className="text-sm text-gray-600">
              <strong className="text-gray-900">{total}</strong> نتيجة لـ
              <strong className="text-blue-700"> "{query}"</strong>
            </p>
            <div className="flex gap-1.5 flex-wrap">
              <button onClick={() => setFT('all')}
                className={`text-xs px-3 py-1 rounded-lg font-medium transition-colors ${filterType==='all'?'bg-gray-900 text-white':'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
                الكل ({results.length})
              </button>
              {facets?.byType?.map(f => (
                <button key={f.label} onClick={() => setFT(f.label)}
                  className={`text-xs px-3 py-1 rounded-lg font-medium transition-colors ${filterType===f.label?'bg-blue-700 text-white':'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
                  {f.icon} {f.label} ({f.count})
                </button>
              ))}
            </div>
          </div>

          {/* Sort */}
          {filtered.length > 0 && (
            <div className="flex items-center gap-2 mb-3">
              <span className="text-xs text-gray-400">{t("sort_by")}</span>
              <span className="text-xs font-semibold text-blue-600">{t("sort_relevance")}</span>
            </div>
          )}

          {/* No results */}
          {filtered.length === 0 && (
            <div className="bg-white rounded-2xl border border-gray-100 p-16 text-center">
              <div className="text-5xl mb-3">🔍</div>
              <p className="font-semibold text-gray-700">t('no_results') لـ "{query}"</p>
              <p className="text-sm text-gray-400 mt-1">{t("try_other")}</p>
              <div className="flex flex-wrap justify-center gap-2 mt-4">
                {QUICK_SEARCHES.map(q => (
                  <button key={q} onClick={() => { setQuery(q); doSearch(q) }}
                    className="text-xs border border-gray-200 text-gray-600 hover:border-blue-400 hover:text-blue-600 px-3 py-1.5 rounded-full">
                    {q}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Results list */}
          <div className="space-y-3">
            {filtered.map(item => (
              <ResultCard
                key={`${item.type}-${item.id}`}
                item={item}
                query={query}
                onClick={() => navigate(item.url)}
              />
            ))}
          </div>
        </>
      )}

      {/* Loading skeleton */}
      {loading && (
        <div className="space-y-3">
          {[1,2,3].map(i => (
            <div key={i} className="bg-white rounded-2xl border border-gray-100 p-4 animate-pulse">
              <div className="flex items-start gap-3">
                <div className="w-10 h-10 bg-gray-200 rounded-xl flex-shrink-0"/>
                <div className="flex-1 space-y-2">
                  <div className="h-4 bg-gray-200 rounded w-2/3"/>
                  <div className="h-3 bg-gray-100 rounded w-full"/>
                  <div className="h-3 bg-gray-100 rounded w-1/2"/>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Empty state — not yet searched */}
      {!searched && !loading && (
        <div className="bg-white rounded-2xl border border-gray-100 p-8 text-center">
          <div className="text-5xl mb-3">🔍</div>
          <p className="font-bold text-gray-700">ابحث في كامل محتوى الدارة</p>
          <div className="grid grid-cols-3 gap-3 mt-6 text-xs text-gray-500">
            {[{icon:'📄',l:'الوثائق'},{icon:'🗂',l:'السجلات'},{icon:'📦',l:'الأصول المعرفية'}].map(t=>(
              <div key={t.l} className="bg-gray-50 rounded-xl p-3">
                <div className="text-2xl mb-1">{t.icon}</div>
                <p>{t.l}</p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
