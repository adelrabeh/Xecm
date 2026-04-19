import React, { useState } from 'react'

const TYPE_PREVIEW = {
  PDF:  { bg:'bg-red-50',    icon:'📕', color:'text-red-600' },
  DOCX: { bg:'bg-blue-50',   icon:'📘', color:'text-blue-600' },
  XLSX: { bg:'bg-green-50',  icon:'📗', color:'text-green-600' },
  PPTX: { bg:'bg-orange-50', icon:'📙', color:'text-orange-600' },
  ZIP:  { bg:'bg-purple-50', icon:'📦', color:'text-purple-600' },
  IMG:  { bg:'bg-pink-50',   icon:'🖼', color:'text-pink-600' },
}

export function PreviewModal({ file, onClose, show }) {
  const [page, setPage] = useState(1)
  const totalPages = file?.pages || 5
  const style = TYPE_PREVIEW[file?.type] || TYPE_PREVIEW.PDF

  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-4xl max-h-[90vh] flex flex-col" onClick={e=>e.stopPropagation()}>

        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-3 border-b border-gray-100">
          <span className="text-2xl">{style.icon}</span>
          <div className="flex-1 min-w-0">
            <p className="font-semibold text-gray-900 text-sm truncate">{file?.name}</p>
            <p className="text-xs text-gray-400">{file?.size} • v{file?.version} • {file?.owner}</p>
          </div>
          <div className="flex gap-2">
            <button onClick={()=>show(`جارٍ تنزيل: ${file?.name}`,'info')}
              className="border border-gray-200 text-gray-600 text-xs px-3 py-1.5 rounded-lg hover:bg-gray-50 flex items-center gap-1">
              ⬇️ تنزيل
            </button>
            <button onClick={()=>show('رابط منسوخ','success')}
              className="border border-gray-200 text-gray-600 text-xs px-3 py-1.5 rounded-lg hover:bg-gray-50">
              🔗
            </button>
            <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl px-1">✕</button>
          </div>
        </div>

        {/* Preview area */}
        <div className={`flex-1 overflow-auto ${style.bg} flex items-center justify-center min-h-[400px]`}>
          <div className="w-full max-w-2xl mx-auto p-6">
            {/* Simulated document */}
            <div className="bg-white rounded-xl shadow-lg p-8 min-h-[500px] relative">
              {/* Watermark */}
              <div className="absolute inset-0 flex items-center justify-center pointer-events-none opacity-5">
                <p className="text-8xl font-black text-gray-900 rotate-[-30deg]">DARAH ECM</p>
              </div>

              <div className="relative z-10">
                <div className="flex items-center gap-3 mb-6 pb-4 border-b border-gray-100">
                  <span className={`text-4xl ${style.color}`}>{style.icon}</span>
                  <div>
                    <p className="font-bold text-gray-900">{file?.name?.replace(/\.[^.]+$/,'')}</p>
                    <p className="text-xs text-gray-400">دارة الملك عبدالعزيز • {file?.modified}</p>
                  </div>
                </div>

                {/* Mock content lines */}
                {Array.from({length:8}).map((_,i)=>(
                  <div key={i} className="mb-3">
                    {i===0 && <div className="h-5 bg-gray-900 rounded w-3/4 mb-4"/>}
                    <div className={`h-3 bg-gray-200 rounded mb-1.5 ${['w-full','w-11/12','w-full','w-10/12','w-full','w-9/12','w-full','w-11/12'][i]}`}/>
                    {i%3===2 && <div className="h-3 w-7/12 bg-gray-200 rounded mb-1.5"/>}
                  </div>
                ))}

                {file?.type === 'XLSX' && (
                  <div className="mt-4 border border-gray-200 rounded-lg overflow-hidden">
                    {Array.from({length:5}).map((_,r)=>(
                      <div key={r} className={`grid grid-cols-4 ${r===0?'bg-gray-100':r%2===0?'bg-gray-50':'bg-white'}`}>
                        {Array.from({length:4}).map((_,c)=>(
                          <div key={c} className="px-3 py-1.5 border-l border-gray-200 text-xs text-gray-600">
                            {r===0?['المنتج','الكمية','السعر','المجموع'][c]:`${r}-${c+1}`}
                          </div>
                        ))}
                      </div>
                    ))}
                  </div>
                )}

                <p className="text-xs text-gray-400 mt-6 text-center">صفحة {page} من {totalPages}</p>
              </div>
            </div>
          </div>
        </div>

        {/* Footer nav */}
        <div className="border-t border-gray-100 px-5 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <button onClick={()=>setPage(p=>Math.max(1,p-1))} disabled={page===1}
              className="px-3 py-1.5 border border-gray-200 rounded-lg text-sm disabled:opacity-40 hover:bg-gray-50 transition-colors">
              ← السابق
            </button>
            <span className="text-xs text-gray-500">{page} / {totalPages}</span>
            <button onClick={()=>setPage(p=>Math.min(totalPages,p+1))} disabled={page===totalPages}
              className="px-3 py-1.5 border border-gray-200 rounded-lg text-sm disabled:opacity-40 hover:bg-gray-50 transition-colors">
              التالي →
            </button>
          </div>
          <div className="flex items-center gap-2 text-xs text-gray-500">
            <span className="bg-gray-100 px-2 py-1 rounded">{file?.classification}</span>
            <span>v{file?.version}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
