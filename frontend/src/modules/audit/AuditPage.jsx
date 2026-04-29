import React, { useState, useEffect } from 'react'
import { useLang } from '../../i18n.js'
import { useToast } from '../../components/Toast'
import client from '../../api/client'

const ACTION_CFG = {
  Create:     { icon:'✅', color:'#059669', bg:'#ecfdf5' },
  Update:     { icon:'✏️', color:'#d97706', bg:'#fffbeb' },
  Delete:     { icon:'🗑️', color:'#dc2626', bg:'#fef2f2' },
  Approve:    { icon:'✅', color:'#2563eb', bg:'#eff6ff' },
  Reject:     { icon:'❌', color:'#dc2626', bg:'#fef2f2' },
  Escalate:   { icon:'🔺', color:'#7c3aed', bg:'#f5f3ff' },
  Login:      { icon:'🔑', color:'#0369a1', bg:'#f0f9ff' },
  Logout:     { icon:'🚪', color:'#6b7280', bg:'#f9fafb' },
  Checkout:   { icon:'🔒', color:'#b45309', bg:'#fffbeb' },
  Archive:    { icon:'🗃️', color:'#4b5563', bg:'#f9fafb' },
  Lock:       { icon:'🔐', color:'#1e40af', bg:'#eff6ff' },
  Download:   { icon:'⬇️', color:'#0891b2', bg:'#ecfeff' },
  Share:      { icon:'🔗', color:'#9333ea', bg:'#faf5ff' },
  default:    { icon:'📋', color:'#6b7280', bg:'#f9fafb' },
}

const MOCK_LOGS = []  // cleared for production

const ENTITY_AR = {
  Record:'سجل', Document:'وثيقة', User:'مستخدم', Task:'مهمة',
  Auth:'تسجيل دخول', Retention:'احتفاظ', TaskEscalation:'تصعيد', default:'—'
}

export default function AuditPage() {
  const { lang, t, fmtDate } = useLang()
  const { show, ToastContainer } = useToast()
  const [logs, setLogs]       = useState(MOCK_LOGS)
  const [loading, setL]       = useState(false)
  const [search, setSearch]   = useState('')
  const [filterEntity, setFE] = useState('all')
  const [filterAction, setFA] = useState('all')
  const [dateFrom, setDF]     = useState('')
  const [dateTo, setDT]       = useState('')
  const [selected, setSelected] = useState(null)
  const [page, setPage]       = useState(1)
  const PAGE_SIZE = 15

  useEffect(() => {
    setL(true)
    client.get('/api/v1/audit', { params: { page:1, pageSize:100 } })
      .then(r => {
        const d = r.data?.data?.items || r.data?.data
        if (Array.isArray(d) && d.length > 0) setLogs(d)
      })
      .catch(() => {})
      .finally(() => setL(false))
  }, [])

  const entities = [...new Set(MOCK_LOGS.map(l => l.entityName))]
  const actions  = [...new Set(MOCK_LOGS.map(l => l.action))]

  const filtered = logs.filter(l => {
    const q = search.toLowerCase()
    const matchQ = !q || l.entityId?.toLowerCase().includes(q) ||
      l.performedByName?.includes(search) || l.action?.toLowerCase().includes(q) ||
      l.newValues?.toLowerCase().includes(q)
    const matchE = filterEntity === 'all' || l.entityName === filterEntity
    const matchA = filterAction === 'all' || l.action === filterAction
    const matchD = (!dateFrom || new Date(l.performedAt) >= new Date(dateFrom)) &&
                   (!dateTo   || new Date(l.performedAt) <= new Date(dateTo + 'T23:59:59Z'))
    return matchQ && matchE && matchA && matchD
  })

  const paged    = filtered.slice((page-1)*PAGE_SIZE, page*PAGE_SIZE)
  const totalPages = Math.ceil(filtered.length / PAGE_SIZE)

  const handleExport = () => {
    const rows = [
      ['التاريخ','الكيان','المعرف','الإجراء','المستخدم','عنوان IP','التفاصيل'],
      ...filtered.map(l => [
        new Date(l.performedAt).toLocaleString('ar-SA'),
        ENTITY_AR[l.entityName]||l.entityName,
        l.entityId, l.action, l.performedByName, l.ipAddress||'—',
        (l.newValues||'').replace(/,/g,' '),
      ])
    ]
    const csv = rows.map(r => r.map(v => `"${v}"`).join(',')).join('\n')
    const blob = new Blob(['\uFEFF'+csv], { type:'text/csv;charset=utf-8' })
    const url  = URL.createObjectURL(blob)
    const a    = document.createElement('a')
    a.href = url; a.download = `audit-log-${new Date().toISOString().split('T')[0]}.csv`
    a.click(); URL.revokeObjectURL(url)
    show(lang==='en'?'Audit log exported':'تم تصدير سجل التدقيق', 'success')
  }

  return (
    <div className="flex flex-col h-full gap-4">
      <ToastContainer/>

      {/* Header */}
      <div className="flex items-center justify-between flex-shrink-0">
        <div>
          <h1 className="text-2xl font-black text-gray-900">
            {lang==='en'?'Audit Log':'سجل التدقيق'}
          </h1>
          <p className="text-sm text-gray-400 mt-0.5">
            {lang==='en'
              ? `${filtered.length} events — immutable audit trail`
              : `${filtered.length} حدث — سجل تدقيق غير قابل للتعديل`}
          </p>
        </div>
        <button onClick={handleExport}
          className="flex items-center gap-2 bg-green-600 text-white px-4 py-2.5 rounded-xl text-sm font-bold hover:bg-green-700 transition-colors shadow-sm">
          📥 {lang==='en'?'Export CSV':'تصدير CSV'}
        </button>
      </div>

      {/* KPI strip */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 flex-shrink-0">
        {[
          { l: lang==='en'?'Total Events':'إجمالي الأحداث', v: logs.length,                     ic:'📋', cls:'bg-indigo-50 text-indigo-700 border-indigo-100' },
          { l: lang==='en'?'Today':'اليوم',                 v: logs.filter(l=>l.performedAt?.startsWith(new Date().toISOString().split('T')[0])).length, ic:'📅', cls:'bg-blue-50 text-blue-700 border-blue-100' },
          { l: lang==='en'?'Locked Records':'سجلات مقفلة', v: logs.filter(l=>l.action?.includes('Lock')||l.action?.includes('Approve')).length, ic:'🔐', cls:'bg-green-50 text-green-700 border-green-100' },
          { l: lang==='en'?'High-Risk Events':'أحداث عالية الخطر', v: logs.filter(l=>['Delete','Escalate'].includes(l.action)).length, ic:'⚠️', cls:'bg-red-50 text-red-700 border-red-100' },
        ].map(k => (
          <div key={k.l} className={`${k.cls} border rounded-2xl p-4 flex items-center gap-3`}>
            <span className="text-2xl">{k.ic}</span>
            <div><p className="text-xl font-black">{k.v}</p><p className="text-[11px] opacity-80">{k.l}</p></div>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="bg-white rounded-2xl border border-gray-100 p-3 flex gap-2 flex-wrap flex-shrink-0">
        <div className="relative flex-1 min-w-36">
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300 text-sm">🔍</span>
          <input value={search} onChange={e=>setSearch(e.target.value)}
            placeholder={lang==='en'?'Search events...':'بحث في الأحداث...'}
            className="w-full pr-9 pl-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"/>
        </div>
        <select value={filterEntity} onChange={e=>{setFE(e.target.value);setPage(1)}}
          className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
          <option value="all">{lang==='en'?'All Entities':'كل الكيانات'}</option>
          {entities.map(e=><option key={e} value={e}>{ENTITY_AR[e]||e}</option>)}
        </select>
        <select value={filterAction} onChange={e=>{setFA(e.target.value);setPage(1)}}
          className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
          <option value="all">{lang==='en'?'All Actions':'كل الإجراءات'}</option>
          {actions.map(a=><option key={a} value={a}>{a}</option>)}
        </select>
        <input type="date" value={dateFrom} onChange={e=>setDF(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none" dir="ltr"/>
        <input type="date" value={dateTo} onChange={e=>setDT(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none" dir="ltr"/>
        {(filterEntity!=='all'||filterAction!=='all'||search||dateFrom||dateTo) && (
          <button onClick={()=>{setFE('all');setFA('all');setSearch('');setDF('');setDT('');setPage(1)}}
            className="border border-gray-200 text-gray-500 px-3 py-2 rounded-xl text-sm hover:bg-gray-50">
            {lang==='en'?'Clear':'مسح'}
          </button>
        )}
      </div>

      {/* Table + Detail */}
      <div className="flex gap-4 flex-1 overflow-hidden min-h-0">
        <div className={`flex-1 overflow-y-auto ${selected?'hidden lg:block':''}`}>
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
            {loading ? (
              <div className="p-8 text-center text-gray-400">
                <div className="text-3xl mb-2 animate-spin inline-block">⏳</div>
                <p>{lang==='en'?'Loading...':'جارٍ التحميل...'}</p>
              </div>
            ) : paged.length === 0 ? (
              <div className="p-12 text-center text-gray-400">
                <div className="text-4xl mb-2">🔍</div>
                <p>{lang==='en'?'No events found':'لا توجد أحداث'}</p>
              </div>
            ) : (
              <>
                <table className="w-full text-sm">
                  <thead>
                    <tr className="bg-gray-50 border-b border-gray-100">
                      <th className="px-4 py-3 text-right text-xs font-black text-gray-400">{lang==='en'?'Time':'الوقت'}</th>
                      <th className="px-4 py-3 text-right text-xs font-black text-gray-400">{lang==='en'?'Action':'الإجراء'}</th>
                      <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">{lang==='en'?'Entity':'الكيان'}</th>
                      <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">{lang==='en'?'User':'المستخدم'}</th>
                      <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden lg:table-cell">{lang==='en'?'Details':'التفاصيل'}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {paged.map(log => {
                      const cfg = ACTION_CFG[log.action] || ACTION_CFG.default
                      return (
                        <tr key={log.id}
                          onClick={() => setSelected(selected?.id===log.id ? null : log)}
                          className={`cursor-pointer transition-colors ${selected?.id===log.id?'bg-blue-50':'hover:bg-gray-50'}`}>
                          <td className="px-4 py-3 text-xs text-gray-500 whitespace-nowrap">
                            {new Date(log.performedAt).toLocaleString(lang==='en'?'en-US':'ar-SA', {month:'short',day:'numeric',hour:'2-digit',minute:'2-digit'})}
                          </td>
                          <td className="px-4 py-3">
                            <span className="inline-flex items-center gap-1.5 text-xs font-bold px-2.5 py-1 rounded-full"
                              style={{background:cfg.bg, color:cfg.color}}>
                              {cfg.icon} {log.action}
                            </span>
                          </td>
                          <td className="px-4 py-3 hidden md:table-cell">
                            <div>
                              <p className="text-xs font-semibold text-gray-700">{ENTITY_AR[log.entityName]||log.entityName}</p>
                              <p className="text-[10px] text-gray-400 font-mono">{log.entityId}</p>
                            </div>
                          </td>
                          <td className="px-4 py-3 hidden md:table-cell text-xs text-gray-600">{log.performedByName}</td>
                          <td className="px-4 py-3 hidden lg:table-cell text-xs text-gray-400 max-w-[200px] truncate">
                            {log.newValues}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>

                {/* Pagination */}
                {totalPages > 1 && (
                  <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 bg-gray-50">
                    <span className="text-xs text-gray-500">
                      {lang==='en'
                        ? `Page ${page} of ${totalPages} — ${filtered.length} events`
                        : `صفحة ${page} من ${totalPages} — ${filtered.length} حدث`}
                    </span>
                    <div className="flex gap-2">
                      <button onClick={()=>setPage(p=>Math.max(1,p-1))} disabled={page===1}
                        className="px-3 py-1.5 border border-gray-200 rounded-lg text-xs text-gray-600 hover:bg-white disabled:opacity-40">
                        {lang==='en'?'← Prev':'← السابق'}
                      </button>
                      <button onClick={()=>setPage(p=>Math.min(totalPages,p+1))} disabled={page===totalPages}
                        className="px-3 py-1.5 border border-gray-200 rounded-lg text-xs text-gray-600 hover:bg-white disabled:opacity-40">
                        {lang==='en'?'Next →':'التالي →'}
                      </button>
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        </div>

        {/* Detail panel */}
        {selected && (
          <div className="w-80 flex-shrink-0 bg-white rounded-2xl border border-gray-100 shadow-sm flex flex-col overflow-hidden">
            <div className="p-4 border-b border-gray-100 flex items-center justify-between">
              <p className="font-bold text-gray-900 text-sm">{lang==='en'?'Event Details':'تفاصيل الحدث'}</p>
              <button onClick={()=>setSelected(null)} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
            </div>
            <div className="p-4 flex-1 overflow-y-auto space-y-3">
              {/* Action badge */}
              <div className="flex justify-center py-2">
                {(() => { const cfg = ACTION_CFG[selected.action]||ACTION_CFG.default; return (
                  <span className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2 rounded-full"
                    style={{background:cfg.bg,color:cfg.color}}>
                    <span className="text-xl">{cfg.icon}</span> {selected.action}
                  </span>
                )})()}
              </div>

              {[
                [lang==='en'?'Date & Time':'التاريخ والوقت', new Date(selected.performedAt).toLocaleString(lang==='en'?'en-US':'ar-SA')],
                [lang==='en'?'Entity Type':'نوع الكيان', ENTITY_AR[selected.entityName]||selected.entityName],
                [lang==='en'?'Entity ID':'معرف الكيان', selected.entityId],
                [lang==='en'?'Action':'الإجراء', selected.action],
                [lang==='en'?'Performed By':'المنفذ', selected.performedByName],
                [lang==='en'?'IP Address':'عنوان IP', selected.ipAddress||'—'],
              ].map(([l,v]) => (
                <div key={l} className="flex justify-between items-start py-2 border-b border-gray-50 text-xs">
                  <span className="text-gray-400 flex-shrink-0">{l}</span>
                  <span className="font-medium text-gray-800 text-right max-w-[160px] break-all">{v}</span>
                </div>
              ))}

              {selected.newValues && (
                <div>
                  <p className="text-xs font-black text-gray-400 uppercase mb-2">{lang==='en'?'Details':'التفاصيل'}</p>
                  <div className="bg-green-50 border border-green-100 rounded-xl p-3 text-xs text-gray-700 leading-relaxed break-all">
                    {selected.newValues}
                  </div>
                </div>
              )}

              {selected.oldValues && (
                <div>
                  <p className="text-xs font-black text-gray-400 uppercase mb-2">{lang==='en'?'Previous Value':'القيمة السابقة'}</p>
                  <div className="bg-red-50 border border-red-100 rounded-xl p-3 text-xs text-gray-700 leading-relaxed break-all">
                    {selected.oldValues}
                  </div>
                </div>
              )}

              <div className="bg-gray-50 rounded-xl p-3 text-xs text-gray-500 text-center">
                🔒 {lang==='en'?'This record is tamper-proof':'هذا السجل غير قابل للتعديل'}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
