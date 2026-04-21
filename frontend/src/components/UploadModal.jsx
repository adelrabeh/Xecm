import { storeFile } from '../hooks/useFileStore'
import React, { useState, useRef } from 'react'
import { useLibraryFilesV2 } from '../hooks/useFolderStore'
import { useFolderStore }    from '../hooks/useFolderStore'
import { addToLibraryV2 }    from '../hooks/useFolderStore'
import client                from '../api/client'

const FILE_ICON = { PDF:'📕', DOCX:'📘', XLSX:'📗', PPTX:'📙', ZIP:'📦', DOC:'📘', XLS:'📗', PNG:'🖼', JPG:'🖼' }
const CLASSIFICATIONS = ['عام','داخلي','سري','سري للغاية']
const STEPS = ['الملف','المجلد','التفاصيل']

export function UploadModal({ onClose, onSuccess, defaultFolderId = null }) {
  const [foldersRaw] = useFolderStore()
  const folders = Array.isArray(foldersRaw) ? foldersRaw : []

  const [step, setStep]           = useState(defaultFolderId ? 0 : 0)
  const [file, setFile]           = useState(null)
  const [selectedFolder, setSF]   = useState(defaultFolderId || '')
  const [drag, setDrag]           = useState(false)
  const [loading, setLoading]     = useState(false)
  const [form, setForm]           = useState({
    name: '', classification: 'داخلي', tags: '', description: ''
  })
  const fileRef = useRef()

  const roots   = folders.filter(f => !f.parent)
  const childOf = (id) => folders.filter(f => f.parent === id)

  const handleFile = (f) => {
    if (!f) return
    setFile(f)
    const nameWithoutExt = f.name.replace(/\.[^.]+$/, '')
    if (!form.name) setForm(p => ({...p, name: nameWithoutExt}))
    setStep(1)
  }

  const handleDrop = (e) => {
    e.preventDefault(); setDrag(false)
    handleFile(e.dataTransfer.files[0])
  }

  const selectedFolderObj = folders.find(f => f.id === selectedFolder)
  const folderPath = () => {
    if (!selectedFolder) return []
    const path = []
    let cur = folders.find(f => f.id === selectedFolder)
    while (cur) { path.unshift(cur); cur = cur.parent ? folders.find(f => f.id === cur.parent) : null }
    return path
  }

  const handleSubmit = async () => {
    if (!file || !selectedFolder || !form.name.trim()) return
    setLoading(true)

    const ext = (file.name.split('.').pop() || 'PDF').toUpperCase()

    try {
      const fd = new FormData()
      fd.append('file', file)
      fd.append('titleAr', form.name)
      fd.append('folderId', selectedFolder)
      await client.post('/api/v1/documents', fd, { headers: {'Content-Type':'multipart/form-data'} })
    } catch {}

    const displayName = form.name.trim() || file.name.replace(/\.[^.]+$/, '')
    const newDoc = {
      id:           'up-' + Date.now(),
      titleAr:      displayName,          // used by DocumentsPage
      title:        displayName,
      name:         displayName + '.' + ext.toLowerCase(),  // full filename with ext
      originalName: file.name,            // original upload filename
      fileType:     ext,
      type:         ext,
      fileSize:     (file.size / 1024 / 1024).toFixed(2) + ' MB',
      size:         (file.size / 1024 / 1024).toFixed(2) + ' MB',
      folder:       selectedFolder,
      folderName:   selectedFolderObj?.name || '',
      folderPath:   folderPath().map(f => f.name).join(' › '),
      owner:        'أنت',
      created:      new Date().toISOString().split('T')[0],
      modified:     new Date().toISOString().split('T')[0],
      createdAt:    new Date().toISOString(),
      updatedAt:    new Date().toISOString(),
      version:      '1.0',
      classification: form.classification,
      tags:         form.tags.split('،').map(t => t.trim()).filter(Boolean),
      description:  form.description,
      summary:      form.description,
      thumb:        FILE_ICON[ext] || '📄',
      status:       'Active',
      isFav:        false,
      isCheckedOut: false,
      likes:        0,
      pages:        1,
      attachments:  [],
      history:      [{ version:'1.0', date: new Date().toISOString().split('T')[0], by:'أنت', action:'رفع جديد' }],
    }

    // Store real file blob for preview/download/print
    const blobUrl = storeFile(newDoc.id, file)
    if (blobUrl) newDoc.blobUrl = blobUrl

    // Save to shared library store
    addToLibraryV2(newDoc)
    setLoading(false)
    onSuccess?.({ msg: `✅ تم رفع "${newDoc.name}" في ${newDoc.folderName}`, doc: newDoc })
    onClose()
  }

  const canNext = [!!file, !!selectedFolder, !!(form.name.trim())]

  const renderFolderOptions = (folder, depth = 0) => (
    <>
      <option key={folder.id} value={folder.id}>
        {'　'.repeat(depth)}{folder.icon || '📁'} {folder.name}
      </option>
      {childOf(folder.id).map(c => renderFolderOptions(c, depth + 1))}
    </>
  )

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg" onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="sticky top-0 bg-white p-4 border-b border-gray-100 z-10">
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-bold text-gray-900">رفع ملف جديد</h2>
            <button onClick={onClose} className="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
          </div>
          {/* Progress */}
          <div className="flex items-center gap-1">
            {STEPS.map((s, i) => (
              <React.Fragment key={i}>
                <div className={`flex items-center gap-1.5 text-xs font-medium ${i < step ? 'text-green-600' : i === step ? 'text-blue-700' : 'text-gray-400'}`}>
                  <div className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold ${i < step ? 'bg-green-500 text-white' : i === step ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-400'}`}>
                    {i < step ? '✓' : i + 1}
                  </div>
                  <span className="hidden sm:block">{s}</span>
                </div>
                {i < STEPS.length - 1 && <div className={`flex-1 h-px mx-1 ${i < step ? 'bg-green-300' : 'bg-gray-100'}`}/>}
              </React.Fragment>
            ))}
          </div>
        </div>

        <div className="p-5">

          {/* ── Step 0: File ── */}
          {step === 0 && (
            <div>
              <div
                onDrop={handleDrop}
                onDragOver={e => { e.preventDefault(); setDrag(true) }}
                onDragLeave={() => setDrag(false)}
                onClick={() => fileRef.current?.click()}
                className={`border-2 border-dashed rounded-2xl p-10 text-center cursor-pointer transition-all ${drag ? 'border-blue-400 bg-blue-50' : 'border-gray-200 hover:border-blue-300 hover:bg-gray-50'}`}>
                <input ref={fileRef} type="file"
                  accept=".pdf,.docx,.xlsx,.pptx,.doc,.xls,.zip,.jpg,.png"
                  className="hidden"
                  onChange={e => handleFile(e.target.files[0])}/>
                <div className="text-5xl mb-3">📁</div>
                <p className="font-semibold text-gray-700">اسحب وأفلت الملف هنا</p>
                <p className="text-sm text-gray-400 mt-1">أو اضغط للاختيار</p>
                <p className="text-xs text-gray-300 mt-2">PDF, DOCX, XLSX, PPTX, ZIP — حتى 50MB</p>
              </div>
            </div>
          )}

          {/* ── Step 1: Folder selection ── */}
          {step === 1 && (
            <div className="space-y-4">
              {/* File preview */}
              {file && (
                <div className="flex items-center gap-3 p-3 bg-green-50 border border-green-200 rounded-xl">
                  <span className="text-2xl">{FILE_ICON[(file.name.split('.').pop()||'').toUpperCase()] || '📄'}</span>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-800 truncate">{file.name}</p>
                    <p className="text-xs text-gray-400">{(file.size/1024/1024).toFixed(2)} MB</p>
                  </div>
                  <span className="text-green-500 text-lg">✓</span>
                </div>
              )}

              <div>
                <label className="block text-sm font-bold text-gray-800 mb-2">
                  اختر المجلد <span className="text-red-400">*</span>
                </label>
                <p className="text-xs text-gray-400 mb-3">حدد المجلد الذي تريد حفظ الملف فيه من هيكل المكتبة</p>

                {/* Folder tree visual picker */}
                <div className="border border-gray-200 rounded-xl overflow-hidden max-h-64 overflow-y-auto">
                  {roots.length === 0 ? (
                    <div className="p-6 text-center text-gray-400 text-sm">
                      <div className="text-3xl mb-2">📁</div>
                      <p>لا توجد مجلدات في المكتبة</p>
                      <p className="text-xs mt-1">يجب على المدير إنشاء مجلدات أولاً</p>
                    </div>
                  ) : (
                    <div className="divide-y divide-gray-50">
                      {roots.map(folder => <FolderPickerItem key={folder.id} folder={folder} folders={folders} selected={selectedFolder} onSelect={setSF} depth={0}/>)}
                    </div>
                  )}
                </div>

                {/* Selected folder path */}
                {selectedFolder && (
                  <div className="mt-2 flex items-center gap-1.5 p-2.5 bg-blue-50 border border-blue-200 rounded-xl">
                    <span className="text-blue-500 text-sm">📍</span>
                    <p className="text-xs font-semibold text-blue-700">
                      {folderPath().map(f => `${f.icon} ${f.name}`).join(' › ')}
                    </p>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* ── Step 2: Details ── */}
          {step === 2 && (
            <div className="space-y-4">
              {/* Path reminder */}
              {selectedFolderObj && (
                <div className="flex items-center gap-2 p-3 bg-blue-50 border border-blue-200 rounded-xl">
                  <span className="text-lg">{selectedFolderObj.icon || '📁'}</span>
                  <div>
                    <p className="text-xs font-bold text-blue-800">سيُحفظ في:</p>
                    <p className="text-xs text-blue-600">{folderPath().map(f => f.name).join(' › ')}</p>
                  </div>
                </div>
              )}

              <div>
                <label className="block text-xs font-bold text-gray-700 mb-1.5">اسم الملف <span className="text-red-400">*</span></label>
                <input value={form.name} onChange={e => setForm(p => ({...p, name: e.target.value}))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
                  placeholder="اسم الملف بدون الامتداد..."/>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1.5">التصنيف الأمني</label>
                  <div className="space-y-1">
                    {CLASSIFICATIONS.map(c => (
                      <button key={c} onClick={() => setForm(p => ({...p, classification: c}))}
                        className={`w-full text-xs px-2.5 py-1.5 rounded-lg border text-right transition-all font-medium ${form.classification === c ? 'border-blue-400 bg-blue-50 text-blue-700' : 'border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
                        {c}
                      </button>
                    ))}
                  </div>
                </div>
                <div>
                  <div className="mb-3">
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">الوسوم</label>
                    <input value={form.tags} onChange={e => setForm(p => ({...p, tags: e.target.value}))}
                      placeholder="وسم1، وسم2، ..."
                      className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">ملاحظات</label>
                    <textarea value={form.description} onChange={e => setForm(p => ({...p, description: e.target.value}))}
                      rows={3} className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"
                      placeholder="وصف مختصر..."/>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="sticky bottom-0 bg-white border-t border-gray-100 p-4 flex gap-3">
          {step > 0 && (
            <button onClick={() => setStep(s => s - 1)}
              className="border border-gray-200 text-gray-600 px-4 py-2.5 rounded-xl text-sm hover:bg-gray-50 transition-colors">
              ← السابق
            </button>
          )}
          {step < STEPS.length - 1 ? (
            <button onClick={() => canNext[step] && setStep(s => s + 1)} disabled={!canNext[step]}
              className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
              التالي →
            </button>
          ) : (
            <button onClick={handleSubmit} disabled={loading || !canNext[2]}
              className="flex-1 bg-green-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-green-700 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors">
              {loading ? '⏳ جارٍ الرفع...' : '📤 رفع الملف'}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

// ─── Folder Picker Item (tree node) ─────────────────────────────────────────
function FolderPickerItem({ folder, folders, selected, onSelect, depth }) {
  const [open, setOpen] = useState(false)
  const children = folders.filter(f => f.parent === folder.id)
  const isSelected = selected === folder.id

  return (
    <>
      <div
        className={`flex items-center gap-2 px-3 py-2.5 cursor-pointer transition-colors ${isSelected ? 'bg-blue-50' : 'hover:bg-gray-50'}`}
        style={{ paddingRight: 12 + depth * 20 }}
        onClick={() => { onSelect(folder.id); if (children.length) setOpen(p => !p) }}>
        {children.length > 0 ? (
          <span className={`text-[10px] text-gray-400 w-3 transition-transform ${open ? 'rotate-90' : ''}`}>▶</span>
        ) : (
          <span className="w-3"/>
        )}
        <span className="text-base">{folder.icon || '📁'}</span>
        <span className={`flex-1 text-sm font-medium ${isSelected ? 'text-blue-700 font-bold' : 'text-gray-700'}`}>
          {folder.name}
        </span>
        {children.length > 0 && (
          <span className="text-[10px] text-gray-400">{children.length} مجلد فرعي</span>
        )}
        {isSelected && <span className="text-blue-500 text-sm">✓</span>}
      </div>
      {(open || isSelected) && children.map(c => (
        <FolderPickerItem key={c.id} folder={c} folders={folders} selected={selected} onSelect={onSelect} depth={depth + 1}/>
      ))}
    </>
  )
}
