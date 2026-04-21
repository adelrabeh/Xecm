import React, { useState, useEffect, useRef } from 'react'
import { getFileBlob } from '../hooks/useFileStore'

const TYPE_STYLE = {
  PDF:  { bg:'#fff1f0', icon:'📕', accent:'#dc2626', label:'PDF Document' },
  DOCX: { bg:'#eff6ff', icon:'📘', accent:'#2563eb', label:'Word Document' },
  DOC:  { bg:'#eff6ff', icon:'📘', accent:'#2563eb', label:'Word Document' },
  XLSX: { bg:'#f0fdf4', icon:'📗', accent:'#16a34a', label:'Excel Spreadsheet' },
  XLS:  { bg:'#f0fdf4', icon:'📗', accent:'#16a34a', label:'Excel Spreadsheet' },
  PPTX: { bg:'#fff7ed', icon:'📙', accent:'#ea580c', label:'PowerPoint' },
  ZIP:  { bg:'#faf5ff', icon:'📦', accent:'#9333ea', label:'Archive' },
  PNG:  { bg:'#fdf2f8', icon:'🖼', accent:'#db2777', label:'Image' },
  JPG:  { bg:'#fdf2f8', icon:'🖼', accent:'#db2777', label:'Image' },
  JPEG: { bg:'#fdf2f8', icon:'🖼', accent:'#db2777', label:'Image' },
}

// ─── Mock content for when no real file is available ──────────────────────────
function MockDocument({ file, page, totalPages }) {
  const type  = (file?.fileType || file?.type || 'PDF').toUpperCase()
  const name  = file?.titleAr || file?.name || file?.title || 'الوثيقة'
  const style = TYPE_STYLE[type] || TYPE_STYLE.PDF

  if (['PNG','JPG','JPEG'].includes(type)) return (
    <div className="h-full flex items-center justify-center bg-gray-900">
      <div className="text-center text-white/60 space-y-3">
        <div className="text-7xl">🖼</div>
        <p className="text-lg font-semibold">{name}</p>
        <p className="text-sm">معاينة الصورة</p>
      </div>
    </div>
  )

  if (['XLSX','XLS'].includes(type)) return (
    <div className="h-full overflow-auto bg-white p-4">
      <div className="rounded-xl overflow-hidden border border-green-200 shadow-sm">
        <div className="bg-green-700 text-white px-4 py-2.5 text-sm font-bold flex items-center gap-2">
          📗 <span className="flex-1">{name}</span>
          <span className="text-green-200 text-xs">الصفحة {page}</span>
        </div>
        <table className="w-full text-xs border-collapse">
          <thead><tr className="bg-green-50">
            {['#','البيان','الكمية','السعر (ر.س)','الإجمالي','الملاحظات'].map(h=>(
              <th key={h} className="px-3 py-2 text-right font-bold text-green-800 border border-green-200">{h}</th>
            ))}
          </tr></thead>
          <tbody>
            {Array.from({length:12}).map((_,i)=>(
              <tr key={i} className={i%2===0?'bg-white':'bg-green-50/40'}>
                <td className="px-3 py-2 border border-gray-100 text-center text-gray-400">{i+1+(page-1)*12}</td>
                <td className="px-3 py-2 border border-gray-100">بند {String.fromCharCode(0x0623+i)}</td>
                <td className="px-3 py-2 border border-gray-100 text-center">{10+i*3}</td>
                <td className="px-3 py-2 border border-gray-100 text-left font-mono">{(150+i*25).toFixed(2)}</td>
                <td className="px-3 py-2 border border-gray-100 text-left font-mono font-semibold">{((150+i*25)*(10+i*3)).toFixed(2)}</td>
                <td className="px-3 py-2 border border-gray-100 text-gray-400">—</td>
              </tr>
            ))}
          </tbody>
          <tfoot><tr className="bg-green-100 font-black text-green-900">
            <td colSpan={4} className="px-3 py-2.5 border border-green-300 text-right">الإجمالي الكلي</td>
            <td className="px-3 py-2.5 border border-green-300 text-left font-mono">
              {Array.from({length:12}).reduce((s,_,i)=>s+((150+i*25)*(10+i*3)),0).toFixed(2)}
            </td>
            <td className="border border-green-300"/>
          </tr></tfoot>
        </table>
      </div>
    </div>
  )

  if (type === 'PPTX') return (
    <div className="h-full flex items-center justify-center p-8" style={{background:'#1e293b'}}>
      <div className="rounded-2xl overflow-hidden shadow-2xl bg-white" style={{width:'100%',maxWidth:640,aspectRatio:'16/9',display:'flex',flexDirection:'column'}}>
        <div className="flex-shrink-0 p-8 flex flex-col justify-end" style={{background:'linear-gradient(135deg,#1e40af,#1e3a8a)',minHeight:'40%'}}>
          <p className="text-2xl font-black text-white leading-tight">{name}</p>
          <p className="text-blue-300 text-sm mt-1.5">الشريحة {page} من {totalPages} · دارة الملك عبدالعزيز</p>
        </div>
        <div className="flex-1 p-6 grid grid-cols-3 gap-3">
          {[1,2,3].map(i=>(
            <div key={i} className="bg-blue-50 border border-blue-100 rounded-xl p-4 flex flex-col gap-2">
              <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center text-white text-sm font-black">{i}</div>
              <div className="h-3 bg-blue-200 rounded w-3/4"/>
              <div className="h-2 bg-gray-200 rounded"/>
              <div className="h-2 bg-gray-200 rounded w-4/5"/>
            </div>
          ))}
        </div>
      </div>
    </div>
  )

  if (type === 'ZIP') return (
    <div className="h-full bg-gray-900 p-6 overflow-auto font-mono text-sm">
      <div className="text-green-400 mb-4">📦 {name}</div>
      <div className="text-gray-600 mb-2 text-xs">Archive contents:</div>
      {Array.from({length:15}).map((_,i)=>(
        <div key={i} className="flex items-center gap-4 py-1.5 border-b border-gray-800">
          <span className="text-gray-600">📄</span>
          <span className="text-gray-300 flex-1">document_{i+1+(page-1)*15}.{['pdf','docx','xlsx','jpg'][i%4]}</span>
          <span className="text-gray-500 text-xs">{(Math.random()*5+0.1).toFixed(1)} MB</span>
          <span className="text-gray-600 text-xs">2026-04-{String(10+i%20).padStart(2,'0')}</span>
        </div>
      ))}
    </div>
  )

  // PDF / DOCX — realistic document
  return (
    <div className="h-full overflow-auto" style={{background:style.bg}}>
      <div className="max-w-[680px] mx-auto my-6 bg-white rounded-xl shadow-lg overflow-hidden">
        {/* Document header */}
        <div className="px-10 pt-8 pb-6 border-b border-gray-100">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-xl flex items-center justify-center text-2xl" style={{background:style.bg}}>
              {style.icon}
            </div>
            <div>
              <p className="text-[10px] text-gray-400 uppercase tracking-wider">دارة الملك عبدالعزيز — نظام ECM</p>
              <p className="text-xs font-semibold text-gray-500">{style.label} · {file?.classification||'داخلي'} · v{file?.version||'1.0'}</p>
            </div>
            <div className="ml-auto text-left text-[10px] text-gray-400">
              <p>{file?.createdAt ? new Date(file.createdAt).toLocaleDateString('ar-SA') : ''}</p>
              <p className="font-bold text-gray-600">صفحة {page} / {totalPages}</p>
            </div>
          </div>
          <h1 className="text-xl font-black text-gray-900 leading-tight text-right">{name}</h1>
          {file?.owner && <p className="text-sm text-gray-500 mt-1 text-right">المُعِد: {file.owner}</p>}
        </div>

        {/* Content */}
        <div className="px-10 py-6 space-y-5">
          {/* Watermark */}
          <div className="absolute inset-0 flex items-center justify-center pointer-events-none opacity-[0.025] overflow-hidden">
            <p className="text-8xl font-black text-gray-900 rotate-[-30deg] whitespace-nowrap">دارة الملك عبدالعزيز</p>
          </div>

          {/* Summary box if available */}
          {file?.summary && (
            <div className="bg-gray-50 border-r-4 rounded-lg p-4 text-sm text-gray-600 text-right leading-relaxed"
              style={{borderColor:style.accent}}>
              {file.summary}
            </div>
          )}

          {/* Mock paragraphs */}
          {Array.from({length:4}).map((_,s)=>(
            <div key={s} className="space-y-2 text-right">
              <div className="h-3.5 rounded" style={{background:'#1f2937', width: s===0?'50%':'40%'}}/>
              <div className="space-y-1.5">
                <div className="h-2.5 bg-gray-200 rounded w-full"/>
                <div className="h-2.5 bg-gray-200 rounded" style={{width:'95%'}}/>
                <div className="h-2.5 bg-gray-200 rounded" style={{width:'88%'}}/>
                {s%2===0 && <div className="h-2.5 bg-gray-100 rounded" style={{width:'72%'}}/>}
              </div>
            </div>
          ))}

          {/* Page info box */}
          <div className="border rounded-lg p-3 text-xs text-gray-400 flex items-center gap-2" style={{borderColor:style.accent+'40'}}>
            <span style={{color:style.accent}}>ℹ</span>
            <span>هذه معاينة محاكاة · ارفع الملف للحصول على المحتوى الفعلي</span>
          </div>
        </div>

        {/* Footer */}
        <div className="px-10 py-4 border-t border-dashed border-gray-200 flex justify-between text-[10px] text-gray-400">
          <span>دارة الملك عبدالعزيز</span>
          <span>سري · للاستخدام الداخلي</span>
          <span>{file?.owner || 'مدير النظام'} — صفحة {page}</span>
        </div>
      </div>
    </div>
  )
}

// ─── PreviewModal ─────────────────────────────────────────────────────────────
export function PreviewModal({ file, onClose }) {
  const [page, setPage]   = useState(1)
  const [zoom, setZoom]   = useState(100)
  const [mode, setMode]   = useState('mock')  // 'real' | 'mock'
  const iframeRef = useRef()

  const type       = (file?.fileType || file?.type || 'PDF').toUpperCase()
  const style      = TYPE_STYLE[type] || TYPE_STYLE.PDF
  const title      = file?.titleAr || file?.name || file?.title || 'معاينة'
  const totalPages = file?.pages || (type==='PPTX'?12 : type==='XLSX'?5 : type==='ZIP'?1 : 8)

  // Try to get real file blob
  const blob = file?.id ? getFileBlob(file.id) : null
  const blobUrl = blob?.url || file?.blobUrl || null
  const isImage = ['PNG','JPG','JPEG'].includes(type)
  const isPDF   = type === 'PDF'

  useEffect(() => {
    setPage(1)
    setZoom(100)
    // Use real preview if blob available
    setMode(blobUrl ? 'real' : 'mock')
  }, [file?.id, blobUrl])

  const handleDownload = () => {
    if (blobUrl) {
      const a = document.createElement('a')
      a.href = blobUrl
      a.download = file?.name || file?.originalName || title
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
    } else {
      // Fallback: open API endpoint
      window.open(`/api/v1/documents/${file?.id}/download`, '_blank')
    }
  }

  const handlePrint = () => {
    if (blobUrl) {
      // Open in new window and print
      const win = window.open(blobUrl, '_blank')
      if (win) {
        win.onload = () => {
          win.focus()
          win.print()
        }
      }
    } else {
      // Print the preview content
      const printWin = window.open('', '_blank')
      if (printWin) {
        printWin.document.write(`
          <html dir="rtl"><head>
            <title>${title}</title>
            <style>
              body { font-family: 'Segoe UI', Arial, sans-serif; direction: rtl; padding: 40px; color: #1f2937; }
              h1 { font-size: 24px; font-weight: 900; margin-bottom: 8px; }
              .meta { color: #6b7280; font-size: 12px; margin-bottom: 24px; }
              .content { line-height: 2; color: #374151; }
              .footer { margin-top: 40px; padding-top: 16px; border-top: 1px dashed #d1d5db; font-size: 11px; color: #9ca3af; display: flex; justify-content: space-between; }
              @media print { body { padding: 20px; } }
            </style>
          </head><body>
            <h1>${title}</h1>
            <div class="meta">
              دارة الملك عبدالعزيز · ${file?.classification || 'داخلي'} · ${file?.owner || ''} · ${new Date().toLocaleDateString('ar-SA')}
            </div>
            <div class="content">
              ${file?.summary ? `<p>${file.summary}</p>` : ''}
              <p>هذه نسخة للطباعة من الوثيقة: <strong>${title}</strong></p>
              <p>رقم الوثيقة: ${file?.id || '—'}</p>
              <p>النوع: ${type} · الإصدار: ${file?.version || '1.0'}</p>
              ${(file?.tags || []).length > 0 ? `<p>الكلمات المفتاحية: ${file.tags.join('، ')}</p>` : ''}
            </div>
            <div class="footer">
              <span>دارة الملك عبدالعزيز</span>
              <span>نظام ECM</span>
              <span>${new Date().toLocaleDateString('ar-SA')}</span>
            </div>
          </body></html>
        `)
        printWin.document.close()
        printWin.focus()
        setTimeout(() => printWin.print(), 500)
      }
    }
  }

  return (
    <div className="fixed inset-0 bg-black/80 flex items-center justify-center z-[100] p-3"
      onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="bg-white rounded-2xl shadow-2xl flex flex-col overflow-hidden"
        style={{width:'min(92vw, 960px)', height:'90vh'}}>

        {/* ── Header toolbar ── */}
        <div className="flex items-center gap-2 px-4 py-3 border-b border-gray-100 bg-gray-50 flex-shrink-0">
          <div className="w-8 h-8 rounded-lg flex items-center justify-center text-lg flex-shrink-0"
            style={{background:style.bg}}>
            {style.icon}
          </div>
          <div className="flex-1 min-w-0">
            <p className="font-bold text-gray-900 text-sm truncate">{title}</p>
            <p className="text-[10px] text-gray-400">
              {style.label} · {file?.fileSize || file?.size || ''} · v{file?.version || '1.0'}
              {file?.classification && ` · ${file.classification}`}
              {blobUrl && <span className="text-green-500 font-semibold"> · ✓ الملف الأصلي</span>}
            </p>
          </div>

          {/* Mode toggle (only when blob available) */}
          {blobUrl && (
            <div className="flex border border-gray-200 rounded-xl overflow-hidden text-xs">
              <button onClick={()=>setMode('real')}
                className={`px-3 py-1.5 font-medium transition-colors ${mode==='real'?'bg-blue-700 text-white':'text-gray-500 hover:bg-gray-100'}`}>
                الملف الأصلي
              </button>
              <button onClick={()=>setMode('mock')}
                className={`px-3 py-1.5 font-medium transition-colors ${mode==='mock'?'bg-gray-700 text-white':'text-gray-500 hover:bg-gray-100'}`}>
                محاكاة
              </button>
            </div>
          )}

          {/* Zoom (mock only) */}
          {mode === 'mock' && (
            <div className="hidden sm:flex items-center gap-1 border border-gray-200 rounded-xl overflow-hidden">
              <button onClick={()=>setZoom(z=>Math.max(50,z-25))}
                className="px-2.5 py-1.5 text-gray-500 hover:bg-gray-100 text-sm font-bold">−</button>
              <span className="px-2 text-xs text-gray-600 w-10 text-center">{zoom}%</span>
              <button onClick={()=>setZoom(z=>Math.min(200,z+25))}
                className="px-2.5 py-1.5 text-gray-500 hover:bg-gray-100 text-sm font-bold">+</button>
            </div>
          )}

          <button onClick={handleDownload}
            className="flex items-center gap-1.5 bg-blue-700 text-white text-xs px-3 py-1.5 rounded-xl hover:bg-blue-800 transition-colors font-semibold">
            ⬇️ تنزيل
          </button>
          <button onClick={handlePrint}
            className="flex items-center gap-1.5 border border-gray-200 text-gray-600 text-xs px-3 py-1.5 rounded-xl hover:bg-gray-100 transition-colors">
            🖨️ طباعة
          </button>
          <button onClick={onClose}
            className="w-8 h-8 rounded-xl hover:bg-gray-200 flex items-center justify-center text-gray-500 text-lg ml-1">
            ✕
          </button>
        </div>

        {/* ── Content area ── */}
        <div className="flex-1 overflow-hidden relative">
          {/* Real file preview via iframe */}
          {mode === 'real' && blobUrl && (
            <div className="h-full">
              {isPDF && (
                <iframe ref={iframeRef} src={blobUrl} className="w-full h-full border-0"/>
              )}
              {isImage && (
                <div className="h-full flex items-center justify-center bg-gray-900 p-8">
                  <img src={blobUrl} alt={title}
                    className="max-h-full max-w-full object-contain rounded-xl shadow-2xl"/>
                </div>
              )}
              {!isPDF && !isImage && (
                <div className="h-full flex flex-col items-center justify-center bg-gray-50 gap-4">
                  <div className="text-5xl">{style.icon}</div>
                  <p className="font-semibold text-gray-700">{title}</p>
                  <p className="text-sm text-gray-500">معاينة هذا النوع غير متاحة — استخدم تنزيل لفتح الملف</p>
                  <button onClick={handleDownload}
                    className="bg-blue-700 text-white px-6 py-2.5 rounded-xl font-bold hover:bg-blue-800 transition-colors">
                    ⬇️ تنزيل الملف لفتحه
                  </button>
                </div>
              )}
            </div>
          )}

          {/* Mock preview */}
          {mode === 'mock' && (
            <div className="h-full overflow-auto"
              style={{transform:`scale(${zoom/100})`, transformOrigin:'top center',
                height: zoom > 100 ? `${100 * 100 / zoom}%` : '100%'}}>
              <MockDocument file={file} page={page} totalPages={totalPages}/>
            </div>
          )}
        </div>

        {/* ── Footer toolbar ── */}
        <div className="flex items-center justify-between px-4 py-2 border-t border-gray-100 bg-gray-50 flex-shrink-0">
          {/* Page nav */}
          <div className="flex items-center gap-2">
            <button onClick={()=>setPage(p=>Math.max(1,p-1))} disabled={page===1}
              className="w-7 h-7 rounded-lg border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-white disabled:opacity-30 text-sm">←</button>
            <span className="text-xs text-gray-500">
              صفحة <strong>{page}</strong> / <strong>{totalPages}</strong>
            </span>
            <button onClick={()=>setPage(p=>Math.min(totalPages,p+1))} disabled={page===totalPages}
              className="w-7 h-7 rounded-lg border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-white disabled:opacity-30 text-sm">→</button>
          </div>

          {/* Tags */}
          <div className="flex items-center gap-1.5 overflow-hidden">
            {(file?.tags||[]).slice(0,4).map(t=>(
              <span key={t} className="text-[10px] bg-white border border-gray-200 text-gray-500 px-2 py-0.5 rounded-full whitespace-nowrap">#{t}</span>
            ))}
          </div>

          {/* Info */}
          <p className="text-[10px] text-gray-400 whitespace-nowrap">
            {file?.owner && `${file.owner} · `}
            {file?.createdAt ? new Date(file.createdAt).toLocaleDateString('ar-SA') : ''}
          </p>
        </div>
      </div>
    </div>
  )
}
