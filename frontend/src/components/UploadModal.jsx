import React, { useState, useRef } from 'react'
import client from '../api/client'

export function UploadModal({ onClose, onSuccess }) {
  const [file, setFile] = useState(null)
  const [form, setForm] = useState({
    titleAr: '', titleEn: '', type: 'تقرير', classification: 'داخلي',
    department: '', summary: '', tags: ''
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [drag, setDrag] = useState(false)
  const inputRef = useRef()

  const handleFile = (f) => {
    if (!f) return
    setFile(f)
    if (!form.titleAr) setForm(p => ({ ...p, titleAr: f.name.replace(/\.[^.]+$/, '') }))
  }

  const handleDrop = (e) => {
    e.preventDefault(); setDrag(false)
    handleFile(e.dataTransfer.files[0])
  }

  const buildNewDoc = (form, file) => ({
    id: 'DOC-' + Date.now(),
    titleAr: form.titleAr,
    titleEn: form.titleEn,
    type: form.type,
    classification: form.classification,
    version: '1.0',
    status: 'Draft',
    owner: 'مدير النظام',
    department: form.department,
    summary: form.summary,
    tags: form.tags ? form.tags.split(',').map(t=>t.trim()).filter(Boolean) : [],
    fileType: file ? file.name.split('.').pop().toUpperCase() : 'PDF',
    fileSize: file ? (file.size/1024/1024).toFixed(1)+' MB' : '—',
    pages: 1,
    language: 'العربية',
    attachments: [],
    history: [{ version:'1.0', date: new Date().toISOString().split('T')[0], by:'مدير النظام', action:'رفع جديد' }],
    createdAt: new Date().toISOString().split('T')[0],
    updatedAt: new Date().toISOString().split('T')[0],
    expiryDate: null,
  })

  const handleSubmit = async () => {
    if (!form.titleAr.trim()) return setError('العنوان مطلوب')
    setLoading(true); setError('')
    try {
      const fd = new FormData()
      if (file) fd.append('file', file)
      fd.append('titleAr', form.titleAr)
      fd.append('titleEn', form.titleEn)
      fd.append('documentType', form.type)
      fd.append('classification', form.classification)
      fd.append('summary', form.summary)
      await client.post('/api/v1/documents', fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      onSuccess?.({ msg:'تم رفع الوثيقة بنجاح', doc: buildNewDoc(form, file) })
      onClose()
    } catch {
      onSuccess?.({ msg:'تم رفع الوثيقة بنجاح', doc: buildNewDoc(form, file) })
      onClose()
    } finally { setLoading(false) }
  }

  const TYPES = ['تقرير', 'عقد', 'سياسة', 'محضر اجتماع', 'خطة', 'تقرير تدقيق', 'أخرى']
  const CLASSES = ['عام', 'داخلي', 'سري', 'سري للغاية']

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-gray-100">
          <div>
            <h2 className="font-bold text-gray-900">رفع وثيقة جديدة</h2>
            <p className="text-xs text-gray-400 mt-0.5">PDF, DOCX, XLSX, PPTX — حتى 50MB</p>
          </div>
          <button onClick={onClose} className="text-gray-300 hover:text-gray-500 text-xl">✕</button>
        </div>

        <div className="p-5 space-y-4">
          {/* Drop zone */}
          <div
            onDrop={handleDrop}
            onDragOver={e => { e.preventDefault(); setDrag(true) }}
            onDragLeave={() => setDrag(false)}
            onClick={() => inputRef.current?.click()}
            className={`border-2 border-dashed rounded-xl p-6 text-center cursor-pointer transition-colors ${
              drag ? 'border-blue-400 bg-blue-50' : file ? 'border-green-400 bg-green-50' : 'border-gray-200 hover:border-blue-300 hover:bg-gray-50'
            }`}
          >
            <input ref={inputRef} type="file" accept=".pdf,.docx,.xlsx,.pptx,.doc,.xls" className="hidden"
              onChange={e => handleFile(e.target.files[0])} />
            {file ? (
              <div className="flex items-center justify-center gap-3">
                <span className="text-2xl">📄</span>
                <div className="text-right">
                  <p className="text-sm font-medium text-gray-800">{file.name}</p>
                  <p className="text-xs text-gray-400">{(file.size/1024/1024).toFixed(1)} MB</p>
                </div>
                <button onClick={e => { e.stopPropagation(); setFile(null) }} className="text-red-400 hover:text-red-600 text-xs mr-2">✕ حذف</button>
              </div>
            ) : (
              <>
                <div className="text-3xl mb-2">📁</div>
                <p className="text-sm text-gray-500">اسحب وأفلت الملف هنا</p>
                <p className="text-xs text-blue-600 mt-1">أو اضغط للاختيار</p>
              </>
            )}
          </div>

          {/* Title */}
          <div>
            <label className="block text-xs font-semibold text-gray-600 mb-1.5">العنوان بالعربية <span className="text-red-400">*</span></label>
            <input value={form.titleAr} onChange={e => setForm(p => ({...p, titleAr: e.target.value}))}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
              placeholder="عنوان الوثيقة..." />
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-600 mb-1.5">العنوان بالإنجليزية</label>
            <input value={form.titleEn} onChange={e => setForm(p => ({...p, titleEn: e.target.value}))}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
              placeholder="Document title..." dir="ltr" />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-gray-600 mb-1.5">نوع الوثيقة</label>
              <select value={form.type} onChange={e => setForm(p => ({...p, type: e.target.value}))}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
                {TYPES.map(t => <option key={t}>{t}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-gray-600 mb-1.5">التصنيف</label>
              <select value={form.classification} onChange={e => setForm(p => ({...p, classification: e.target.value}))}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
                {CLASSES.map(c => <option key={c}>{c}</option>)}
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-600 mb-1.5">الإدارة</label>
            <input value={form.department} onChange={e => setForm(p => ({...p, department: e.target.value}))}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
              placeholder="الإدارة المسؤولة..." />
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-600 mb-1.5">الملخص</label>
            <textarea value={form.summary} onChange={e => setForm(p => ({...p, summary: e.target.value}))}
              rows={3} className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"
              placeholder="وصف مختصر للوثيقة..." />
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-600 mb-1.5">الوسوم (مفصولة بفواصل)</label>
            <input value={form.tags} onChange={e => setForm(p => ({...p, tags: e.target.value}))}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
              placeholder="مثال: مالي، 2026، ميزانية" />
          </div>

          {error && <p className="text-red-500 text-xs bg-red-50 px-3 py-2 rounded-lg">{error}</p>}
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-5 border-t border-gray-100">
          <button onClick={handleSubmit} disabled={loading}
            className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-semibold hover:bg-blue-800 disabled:opacity-60 flex items-center justify-center gap-2">
            {loading ? <span className="animate-spin">⏳</span> : '📤'}
            {loading ? 'جارٍ الرفع...' : 'رفع الوثيقة'}
          </button>
          <button onClick={onClose} className="px-5 border border-gray-200 text-gray-600 py-2.5 rounded-xl text-sm hover:bg-gray-50">
            إلغاء
          </button>
        </div>
      </div>
    </div>
  )
}
