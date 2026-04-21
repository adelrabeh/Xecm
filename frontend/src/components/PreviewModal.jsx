import React, { useState, useEffect, useRef } from 'react'

const TYPE_STYLE = {
  PDF:  { bg:'from-red-50 to-red-100',     icon:'📕', accent:'#dc2626', label:'PDF' },
  DOCX: { bg:'from-blue-50 to-blue-100',   icon:'📘', accent:'#2563eb', label:'Word' },
  DOC:  { bg:'from-blue-50 to-blue-100',   icon:'📘', accent:'#2563eb', label:'Word' },
  XLSX: { bg:'from-green-50 to-green-100', icon:'📗', accent:'#16a34a', label:'Excel' },
  XLS:  { bg:'from-green-50 to-green-100', icon:'📗', accent:'#16a34a', label:'Excel' },
  PPTX: { bg:'from-orange-50 to-orange-100',icon:'📙',accent:'#ea580c', label:'PowerPoint' },
  ZIP:  { bg:'from-purple-50 to-purple-100',icon:'📦',accent:'#9333ea', label:'Archive' },
  PNG:  { bg:'from-pink-50 to-pink-100',   icon:'🖼', accent:'#db2777', label:'Image' },
  JPG:  { bg:'from-pink-50 to-pink-100',   icon:'🖼', accent:'#db2777', label:'Image' },
  JPEG: { bg:'from-pink-50 to-pink-100',   icon:'🖼', accent:'#db2777', label:'Image' },
}

// Realistic mock content per file type
function MockContent({ file, page, totalPages }) {
  const type = (file?.fileType || file?.type || 'PDF').toUpperCase()
  const name  = file?.titleAr || file?.name || file?.title || 'الوثيقة'
  const style = TYPE_STYLE[type] || TYPE_STYLE.PDF

  if (['PNG','JPG','JPEG'].includes(type)) return (
    <div className="flex items-center justify-center h-full bg-gray-900 p-8">
      <div className="text-center text-white">
        <div className="text-8xl mb-4">🖼</div>
        <p className="text-lg font-semibold opacity-80">{name}</p>
        <p className="text-sm opacity-50 mt-1">معاينة الصورة غير متاحة في وضع التجريب</p>
      </div>
    </div>
  )

  if (type === 'XLSX' || type === 'XLS') return (
    <div className="h-full bg-white p-4 overflow-auto">
      <div className="bg-green-700 text-white px-4 py-2 text-xs font-bold rounded-t-lg flex items-center gap-2">
        <span>📗</span> جدول البيانات — {name}
        <span className="ml-auto opacity-70">الورقة 1</span>
      </div>
      <div className="border border-green-200 rounded-b-lg overflow-hidden shadow-sm">
        <table className="w-full text-xs border-collapse">
          <thead>
            <tr className="bg-green-50">
              {['#','البيان','الكمية','السعر (ر.س)','الإجمالي'].map(h=>(
                <th key={h} className="px-3 py-2 text-right font-bold text-green-800 border border-green-200">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({length:10}).map((_,i)=>(
              <tr key={i} className={i%2===0?'bg-white':'bg-green-50/30'}>
                <td className="px-3 py-2 border border-gray-100 text-center text-gray-400">{i+1+(page-1)*10}</td>
                <td className="px-3 py-2 border border-gray-100 text-gray-700">بند رقم {i+1+(page-1)*10}</td>
                <td className="px-3 py-2 border border-gray-100 text-center text-gray-600">{Math.floor(Math.random()*100+5)}</td>
                <td className="px-3 py-2 border border-gray-100 text-gray-600 text-left">{(Math.random()*500+100).toFixed(2)}</td>
                <td className="px-3 py-2 border border-gray-100 font-semibold text-gray-800 text-left">{(Math.random()*50000+1000).toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="bg-green-100 font-bold">
              <td colSpan={4} className="px-3 py-2 border border-green-200 text-right text-green-800">الإجمالي الكلي</td>
              <td className="px-3 py-2 border border-green-200 text-left text-green-900">{(Math.random()*500000+50000).toFixed(2)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  )

  if (type === 'PPTX') return (
    <div className="h-full bg-gray-100 flex items-center justify-center p-6">
      <div className="bg-white rounded-2xl shadow-xl overflow-hidden" style={{width:'100%',maxWidth:640,aspectRatio:'16/9'}}>
        <div className="bg-gradient-to-l from-blue-700 to-blue-900 text-white p-8 h-1/3 flex flex-col justify-end">
          <p className="text-xl font-black leading-tight">{name}</p>
          <p className="text-blue-200 text-sm mt-1">الشريحة {page} من {totalPages}</p>
        </div>
        <div className="p-8 flex flex-col gap-4 h-2/3">
          <div className="flex gap-4">
            {[1,2,3].map(i=>(
              <div key={i} className="flex-1 bg-blue-50 border border-blue-100 rounded-xl p-4">
                <div className="w-8 h-8 bg-blue-600 rounded-lg mb-3 flex items-center justify-center text-white text-sm font-bold">{i}</div>
                <div className="h-3 bg-blue-200 rounded mb-2 w-3/4"/>
                <div className="h-2 bg-gray-200 rounded mb-1"/>
                <div className="h-2 bg-gray-200 rounded w-2/3"/>
              </div>
            ))}
          </div>
          <div className="flex-1 bg-gray-50 rounded-xl p-4">
            <div className="h-3 bg-gray-300 rounded mb-2 w-1/2"/>
            <div className="h-2 bg-gray-200 rounded mb-1"/>
            <div className="h-2 bg-gray-200 rounded mb-1 w-4/5"/>
            <div className="h-2 bg-gray-200 rounded w-3/5"/>
          </div>
        </div>
      </div>
    </div>
  )

  if (type === 'ZIP') return (
    <div className="h-full bg-gray-900 p-6 overflow-auto font-mono text-sm">
      <div className="text-green-400 mb-4">📦 محتوى الأرشيف: {name}</div>
      {Array.from({length:8}).map((_,i)=>(
        <div key={i} className="flex items-center gap-3 py-1.5 border-b border-gray-800 text-gray-300">
          <span className="text-gray-500">{'>'}</span>
          <span className="flex-1">ملف_{i+1+(page-1)*8}.{['pdf','docx','xlsx','jpg','png'][i%5]}</span>
          <span className="text-gray-500 text-xs">{(Math.random()*5+0.1).toFixed(1)} MB</span>
        </div>
      ))}
    </div>
  )

  // Default: PDF / DOCX — document look
  const lineCount = type === 'PDF' ? 14 : 12
  return (
    <div className="h-full bg-white overflow-auto">
      <div className="max-w-2xl mx-auto p-8 min-h-full">
        {/* Watermark */}
        <div className="fixed inset-0 flex items-center justify-center pointer-events-none opacity-[0.03] z-0">
          <p className="text-7xl font-black text-gray-900 rotate-[-30deg] whitespace-nowrap select-none">
            دارة الملك عبدالعزيز
          </p>
        </div>

        <div className="relative z-10">
          {/* Document header */}
          <div className="flex items-start gap-4 pb-5 mb-6 border-b border-gray-200">
            <div className="w-14 h-14 rounded-2xl flex items-center justify-center text-3xl flex-shrink-0"
              style={{background: style.bg.includes('red') ? '#fef2f2' : style.bg.includes('blue') ? '#eff6ff' : '#f0fdf4'}}>
              {style.icon}
            </div>
            <div className="flex-1">
              <h1 className="text-xl font-black text-gray-900 leading-tight">{name}</h1>
              <div className="flex items-center gap-3 mt-1.5 text-xs text-gray-400">
                <span>دارة الملك عبدالعزيز</span>
                <span>·</span>
                <span>صفحة {page} من {totalPages}</span>
                <span>·</span>
                <span>{file?.version ? `الإصدار ${file.version}` : 'v1.0'}</span>
                {file?.classification && <><span>·</span><span className="font-semibold" style={{color:style.accent}}>{file.classification}</span></>}
              </div>
            </div>
          </div>

          {/* Content lines */}
          <div className="space-y-4">
            {/* First paragraph - title block */}
            <div className="text-center pb-4">
              <div className="h-4 bg-gray-800 rounded mx-auto" style={{width:'60%'}}/>
            </div>

            {Array.from({length: Math.ceil(lineCount/3)}).map((_,p)=>(
              <div key={p} className="space-y-2">
                {p===0 && <div className="h-3 bg-gray-700 rounded" style={{width:'45%'}}/>}
                <div className="h-2.5 bg-gray-200 rounded" style={{width:'100%'}}/>
                <div className="h-2.5 bg-gray-200 rounded" style={{width:'95%'}}/>
                <div className="h-2.5 bg-gray-200 rounded" style={{width:'88%'}}/>
                {p%2===0 && <div className="h-2.5 bg-gray-100 rounded" style={{width:'70%'}}/>}
              </div>
            ))}

            {/* Summary box */}
            <div className="border-r-4 pl-4 py-3 bg-gray-50 rounded-lg mt-6" style={{borderColor:style.accent}}>
              <div className="h-2.5 bg-gray-300 rounded mb-2 w-3/4"/>
              <div className="h-2.5 bg-gray-200 rounded mb-1"/>
              <div className="h-2.5 bg-gray-200 rounded w-4/5"/>
            </div>

            {/* More paragraphs */}
            {Array.from({length:2}).map((_,p)=>(
              <div key={'b'+p} className="space-y-2">
                <div className="h-3 bg-gray-600 rounded" style={{width:'35%'}}/>
                <div className="h-2.5 bg-gray-200 rounded"/>
                <div className="h-2.5 bg-gray-200 rounded" style={{width:'90%'}}/>
                <div className="h-2.5 bg-gray-200 rounded" style={{width:'75%'}}/>
              </div>
            ))}
          </div>

          {/* Footer */}
          <div className="mt-10 pt-4 border-t border-dashed border-gray-200 flex justify-between items-center text-xs text-gray-400">
            <span>{file?.createdAt ? new Date(file.createdAt).toLocaleDateString('ar-SA') : new Date().toLocaleDateString('ar-SA')}</span>
            <span>دارة الملك عبدالعزيز — نظام ECM</span>
            <span>{file?.owner || 'المدير العام'}</span>
          </div>
        </div>
      </div>
    </div>
  )
}

export function PreviewModal({ file, onClose }) {
  const [page, setPage]       = useState(1)
  const [zoom, setZoom]       = useState(100)
  const contentRef = useRef()

  const type       = (file?.fileType || file?.type || 'PDF').toUpperCase()
  const style      = TYPE_STYLE[type] || TYPE_STYLE.PDF
  const title      = file?.titleAr || file?.name || file?.title || 'معاينة'
  const totalPages = file?.pages || (type === 'XLSX' ? 3 : type === 'PPTX' ? 12 : type === 'ZIP' ? 1 : 8)

  // Reset page when file changes
  useEffect(() => { setPage(1); setZoom(100) }, [file?.id])

  const handleDownload = () => {
    // In real system: window.open(`/api/v1/documents/${file.id}/download`)
    const link = document.createElement('a')
    link.href   = '#'
    link.download = file?.name || title
    // link.click()
  }

  return (
    <div className="fixed inset-0 bg-black/80 flex items-center justify-center z-50 p-3" onClick={onClose}>
      <div
        className="bg-white rounded-2xl shadow-2xl flex flex-col overflow-hidden"
        style={{width:'90vw', maxWidth:900, height:'90vh'}}
        onClick={e => e.stopPropagation()}>

        {/* ── Top toolbar ── */}
        <div className="flex items-center gap-3 px-4 py-3 border-b border-gray-100 bg-gray-50 flex-shrink-0">
          {/* File info */}
          <div className="flex items-center gap-2.5 flex-1 min-w-0">
            <div className="w-9 h-9 rounded-xl flex items-center justify-center text-xl flex-shrink-0"
              style={{background: type === 'PDF' ? '#fef2f2' : type.includes('XL') ? '#f0fdf4' : '#eff6ff'}}>
              {style.icon}
            </div>
            <div className="min-w-0">
              <p className="font-bold text-gray-900 text-sm truncate">{title}</p>
              <p className="text-[10px] text-gray-400">
                {style.label} · {file?.fileSize || file?.size || '—'} · v{file?.version || '1.0'}
                {file?.classification && ` · ${file.classification}`}
              </p>
            </div>
          </div>

          {/* Zoom */}
          <div className="hidden sm:flex items-center gap-1 border border-gray-200 rounded-xl overflow-hidden">
            <button onClick={() => setZoom(z => Math.max(50, z-25))}
              className="px-2.5 py-1.5 text-gray-500 hover:bg-gray-100 text-sm font-bold">−</button>
            <span className="px-2 text-xs text-gray-600 min-w-[40px] text-center">{zoom}%</span>
            <button onClick={() => setZoom(z => Math.min(200, z+25))}
              className="px-2.5 py-1.5 text-gray-500 hover:bg-gray-100 text-sm font-bold">+</button>
          </div>

          {/* Actions */}
          <button onClick={handleDownload}
            className="hidden sm:flex items-center gap-1.5 border border-gray-200 text-gray-600 text-xs px-3 py-1.5 rounded-xl hover:bg-gray-100 transition-colors">
            ⬇️ تنزيل
          </button>
          <button onClick={() => window.print?.()}
            className="hidden sm:flex items-center gap-1.5 border border-gray-200 text-gray-600 text-xs px-3 py-1.5 rounded-xl hover:bg-gray-100 transition-colors">
            🖨️ طباعة
          </button>
          <button onClick={onClose}
            className="w-8 h-8 rounded-xl hover:bg-gray-200 flex items-center justify-center text-gray-500 text-lg transition-colors">
            ✕
          </button>
        </div>

        {/* ── Content ── */}
        <div
          ref={contentRef}
          className={`flex-1 overflow-auto bg-gradient-to-br ${style.bg}`}
          style={{transform: `scale(${zoom/100})`, transformOrigin:'top center', minHeight: zoom > 100 ? `${zoom}%` : 'auto'}}>
          <MockContent file={file} page={page} totalPages={totalPages} />
        </div>

        {/* ── Bottom toolbar ── */}
        <div className="flex items-center justify-between px-4 py-2.5 border-t border-gray-100 bg-gray-50 flex-shrink-0">
          {/* Page navigation */}
          <div className="flex items-center gap-2">
            <button onClick={() => setPage(p => Math.max(1, p-1))} disabled={page === 1}
              className="w-8 h-8 rounded-lg border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-white disabled:opacity-30 transition-colors text-sm">
              ←
            </button>
            <span className="text-xs text-gray-500 px-2">
              صفحة <strong className="text-gray-800">{page}</strong> من <strong className="text-gray-800">{totalPages}</strong>
            </span>
            <button onClick={() => setPage(p => Math.min(totalPages, p+1))} disabled={page === totalPages}
              className="w-8 h-8 rounded-lg border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-white disabled:opacity-30 transition-colors text-sm">
              →
            </button>
          </div>

          {/* Tags */}
          <div className="hidden sm:flex items-center gap-1.5 overflow-hidden">
            {(file?.tags || []).slice(0, 3).map(t => (
              <span key={t} className="text-[10px] bg-white border border-gray-200 text-gray-500 px-2 py-0.5 rounded-full">#{t}</span>
            ))}
          </div>

          {/* File metadata */}
          <div className="text-[10px] text-gray-400 text-left hidden md:block">
            {file?.owner && <span>{file.owner} · </span>}
            {file?.createdAt && <span>{new Date(file.createdAt).toLocaleDateString('ar-SA')}</span>}
          </div>
        </div>
      </div>
    </div>
  )
}
