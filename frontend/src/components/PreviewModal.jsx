import React, { useState } from 'react'

const TYPE_STYLE = {
  PDF:  { bg:'from-red-50 to-red-100',    icon:'📕', accent:'text-red-600',    label:'PDF Document' },
  DOCX: { bg:'from-blue-50 to-blue-100',  icon:'📘', accent:'text-blue-600',   label:'Word Document' },
  DOC:  { bg:'from-blue-50 to-blue-100',  icon:'📘', accent:'text-blue-600',   label:'Word Document' },
  XLSX: { bg:'from-green-50 to-green-100',icon:'📗', accent:'text-green-600',  label:'Excel Sheet' },
  PPTX: { bg:'from-orange-50 to-orange-100',icon:'📙',accent:'text-orange-600',label:'Presentation' },
  ZIP:  { bg:'from-purple-50 to-purple-100',icon:'📦',accent:'text-purple-600',label:'Archive' },
  PNG:  { bg:'from-pink-50 to-pink-100',  icon:'🖼', accent:'text-pink-600',   label:'Image' },
  JPG:  { bg:'from-pink-50 to-pink-100',  icon:'🖼', accent:'text-pink-600',   label:'Image' },
}

function MockDocContent({ file, page }) {
  const type = file?.type?.toUpperCase() || 'PDF'
  
  if (type === 'XLSX') return (
    <div className="bg-white rounded-xl shadow-md overflow-hidden">
      <div className="bg-green-600 text-white px-4 py-2 text-xs font-bold">📗 جدول البيانات</div>
      <table className="w-full text-xs">
        <thead><tr className="bg-gray-100">
          {['#','البند','الكمية','السعر','المجموع'].map(h=>(
            <th key={h} className="px-3 py-2 text-right font-bold text-gray-600 border border-gray-200">{h}</th>
          ))}
        </tr></thead>
        <tbody>
          {Array.from({length:8}).map((_,i)=>(
            <tr key={i} className={i%2===0?'bg-white':'bg-gray-50'}>
              {[i+1, `بند ${i+1}`, Math.floor(Math.random()*100+10), `${(Math.random()*500+100).toFixed(0)} ر.س`, `${(Math.random()*50000+5000).toFixed(0)} ر.س`].map((v,j)=>(
                <td key={j} className="px-3 py-2 border border-gray-100 text-right text-gray-700">{v}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )

  if (type === 'PPTX') return (
    <div className="bg-white rounded-xl shadow-md overflow-hidden aspect-video flex flex-col">
      <div className="bg-blue-700 text-white px-6 py-4 flex-shrink-0">
        <p className="text-lg font-black">{file?.name?.replace(/\.[^.]+$/,'') || 'العرض التقديمي'}</p>
        <p className="text-blue-200 text-sm">الشريحة {page} من {file?.pages || 10}</p>
      </div>
      <div className="flex-1 p-6 flex flex-col gap-3">
        <div className="h-4 bg-gray-800 rounded w-2/3"/>
        <div className="flex gap-2 mt-2">
          {[1,2,3].map(i=>(
            <div key={i} className="flex-1 bg-blue-50 border border-blue-200 rounded-lg p-3">
              <div className="h-3 bg-blue-300 rounded mb-2"/>
              <div className="h-2 bg-gray-200 rounded mb-1"/>
              <div className="h-2 bg-gray-200 rounded w-3/4"/>
            </div>
          ))}
        </div>
      </div>
    </div>
  )

  // Default: PDF / DOCX style
  return (
    <div className="bg-white rounded-xl shadow-md p-8 min-h-[480px] relative">
      <div className="absolute inset-0 flex items-center justify-center pointer-events-none opacity-[0.04]">
        <p className="text-7xl font-black text-gray-900 rotate-[-30deg] whitespace-nowrap">دارة الملك عبدالعزيز</p>
      </div>
      <div className="relative z-10 space-y-4">
        {/* Doc header */}
        <div className="border-b border-gray-100 pb-4 flex items-center gap-3">
          <div className="w-12 h-12 bg-blue-50 rounded-xl flex items-center justify-center text-2xl flex-shrink-0">
            {TYPE_STYLE[type]?.icon || '📄'}
          </div>
          <div>
            <p className="font-black text-gray-900 text-lg leading-tight">{file?.name?.replace(/\.[^.]+$/,'') || 'الوثيقة'}</p>
            <p className="text-xs text-gray-400 mt-0.5">دارة الملك عبدالعزيز • صفحة {page} من {file?.pages || 5}</p>
          </div>
        </div>
        {/* Content lines */}
        <div className="space-y-3">
          <div className="h-5 bg-gray-900 rounded w-3/4"/>
          {Array.from({length:6}).map((_,i)=>(
            <div key={i} className="space-y-1.5">
              <div className={`h-3 bg-gray-200 rounded ${['w-full','w-11/12','w-full','w-10/12','w-full','w-9/12'][i]}`}/>
              {i%2===0 && <div className="h-3 bg-gray-100 rounded w-7/12"/>}
            </div>
          ))}
        </div>
        {/* Signature area */}
        <div className="mt-8 pt-4 border-t border-dashed border-gray-200 flex justify-between items-center">
          <div className="text-xs text-gray-400">
            <p>التاريخ: {file?.modified || new Date().toLocaleDateString('ar-SA')}</p>
            <p>الإصدار: v{file?.version || '1.0'}</p>
          </div>
          <div className="text-xs text-gray-400 text-right">
            <p>{file?.classification || 'داخلي'}</p>
            <p>{file?.owner || 'مدير النظام'}</p>
          </div>
        </div>
      </div>
    </div>
  )
}

export function PreviewModal({ file, onClose }) {
  const [page, setPage] = useState(1)
  const totalPages = file?.pages || (file?.type === 'PPTX' ? 12 : 5)
  const type = (file?.type || 'PDF').toUpperCase()
  const style = TYPE_STYLE[type] || TYPE_STYLE.PDF
  const title = file?.name || file?.titleAr || file?.title || 'معاينة الملف'

  const handleDownload = () => {
    const a = document.createElement('a')
    a.href = `/api/v1/documents/${file?.id || file?.documentId}/download`
    a.download = title
    // a.click() — would trigger in real system
  }

  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-4xl max-h-[92vh] flex flex-col" onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-3 border-b border-gray-100 flex-shrink-0">
          <span className="text-2xl">{style.icon}</span>
          <div className="flex-1 min-w-0">
            <p className="font-bold text-gray-900 text-sm truncate">{title}</p>
            <p className="text-xs text-gray-400">{file?.size || '—'} • v{file?.version || '1.0'} • {file?.owner || '—'}</p>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-xs text-gray-400 bg-gray-100 px-2 py-1 rounded-lg">{type}</span>
            {file?.classification && (
              <span className={`text-xs px-2 py-1 rounded-lg font-medium ${
                file.classification==='سري للغاية'?'bg-red-100 text-red-700':
                file.classification==='سري'?'bg-orange-100 text-orange-700':
                file.classification==='داخلي'?'bg-blue-100 text-blue-700':'bg-green-100 text-green-700'
              }`}>{file.classification}</span>
            )}
            <button onClick={handleDownload}
              className="border border-gray-200 text-gray-600 text-xs px-3 py-1.5 rounded-lg hover:bg-gray-50 flex items-center gap-1 transition-colors">
              ⬇️ تنزيل
            </button>
            <button onClick={onClose}
              className="w-8 h-8 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 hover:text-gray-600 text-lg transition-colors">
              ✕
            </button>
          </div>
        </div>

        {/* Preview area */}
        <div className={`flex-1 overflow-auto bg-gradient-to-br ${style.bg} p-6`}>
          <div className="max-w-2xl mx-auto">
            <MockDocContent file={file} page={page} />
          </div>
        </div>

        {/* Footer pagination */}
        <div className="border-t border-gray-100 px-5 py-3 flex items-center justify-between flex-shrink-0">
          <div className="flex items-center gap-2">
            <button onClick={() => setPage(p => Math.max(1, p-1))} disabled={page === 1}
              className="px-3 py-1.5 border border-gray-200 rounded-lg text-sm disabled:opacity-40 hover:bg-gray-50 transition-colors">
              ← السابقة
            </button>
            <span className="text-xs text-gray-500 px-2">
              صفحة <strong>{page}</strong> من <strong>{totalPages}</strong>
            </span>
            <button onClick={() => setPage(p => Math.min(totalPages, p+1))} disabled={page === totalPages}
              className="px-3 py-1.5 border border-gray-200 rounded-lg text-sm disabled:opacity-40 hover:bg-gray-50 transition-colors">
              التالية →
            </button>
          </div>
          <div className="flex items-center gap-3 text-xs text-gray-400">
            {file?.tags?.length > 0 && (
              <div className="flex gap-1">
                {file.tags.slice(0,3).map(t => (
                  <span key={t} className="bg-gray-100 px-2 py-0.5 rounded-full">#{t}</span>
                ))}
              </div>
            )}
            {file?.primarySubject && <span className="text-blue-500">🏷️ {file.primarySubject}</span>}
          </div>
        </div>
      </div>
    </div>
  )
}
