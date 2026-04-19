import { addToLibrary } from '../hooks/useLibraryFiles'
import React, { useState, useRef } from 'react'
import client from '../api/client'

// ─── Taxonomy aligned with library folders ────────────────────────────────────
const TAXONOMY = [
  {
    id: 'ADMIN', label: '🏛️ الوثائق الإدارية', folderId: 'f1',
    subjects: [
      { id: 'STR', label: '🎯 الاستراتيجية والتخطيط',    folderId: 'f1',
        types: ['استراتيجيات','مؤشرات أداء KPIs','مبادرات','خطط تشغيلية','رؤية ورسالة'] },
      { id: 'GOV', label: '⚖️ الحوكمة والامتثال',        folderId: 'f1',
        types: ['السياسات','الإجراءات','اللوائح','التدقيق الداخلي','إدارة المخاطر','الشؤون القانونية'] },
      { id: 'TEC', label: '💻 التقنية والرقمنة',           folderId: 'f1',
        types: ['نظام المعلومات ECM','البنية التحتية','الأمن السيبراني','التحول الرقمي','التكاملات'] },
      { id: 'HR',  label: '👥 الموارد البشرية',           folderId: 'f1',
        types: ['التوظيف','التدريب','الهيكل التنظيمي','تقييم الأداء','التخطيط الوظيفي'] },
      { id: 'FIN', label: '💰 المالية والمشتريات',         folderId: 'f2',
        types: ['الميزانيات','التقارير المالية','العقود','إدارة الموردين','المشتريات'] },
      { id: 'PRJ', label: '📊 المشاريع والبرامج',          folderId: 'f1',
        types: ['ملفات المشاريع','طلبات التغيير','مخاطر المشاريع','PMO'] },
      { id: 'OPS', label: '⚙️ العمليات والخدمات',          folderId: 'f1',
        types: ['العمليات التشغيلية','تقارير الخدمة','اتفاقيات مستوى الخدمة SLA'] },
      { id: 'COM', label: '📢 الاتصال والإعلام',           folderId: 'f1',
        types: ['المراسلات الرسمية','التقارير الإعلامية','الفعاليات والمؤتمرات','النشرات'] },
    ]
  },
  {
    id: 'HIST', label: '📜 الوثائق التاريخية والأرشيفية', folderId: 'f3',
    subjects: [
      { id: 'SAH', label: '🏺 التاريخ السعودي',           folderId: 'f3',
        types: ['الدولة السعودية الأولى','الدولة السعودية الثانية','المملكة الحديثة','التاريخ المعاصر'] },
      { id: 'POL', label: '🏛️ التاريخ السياسي',           folderId: 'f3',
        types: ['أنظمة الحكم','المراسيم الملكية','الشخصيات السياسية','العلاقات الدولية'] },
      { id: 'SOC', label: '🕌 التاريخ الاجتماعي والثقافي',folderId: 'f3',
        types: ['التراث والعادات','الحياة اليومية','الهوية الثقافية','التعليم التاريخي'] },
      { id: 'GEO', label: '🗺️ الجغرافيا والدراسات الإقليمية',folderId:'f3',
        types: ['مناطق المملكة','المدن والمستوطنات','الجغرافيا التاريخية','توزيع القبائل'] },
      { id: 'BIO', label: '👤 التراجم والشخصيات',          folderId: 'f3',
        types: ['ملوك المملكة','الشخصيات التاريخية','العلماء والمفكرون','الأدباء والمؤرخون'] },
      { id: 'MAN', label: '📋 المخطوطات والأرشيف',         folderId: 'f3',
        types: ['الوثائق التاريخية','الرسائل والمراسلات','المخطوطات','السجلات الحكومية'] },
      { id: 'HER', label: '🏰 التراث والآثار',             folderId: 'f4',
        types: ['المواقع الأثرية','المتاحف','التراث العمراني','المناطق التاريخية'] },
      { id: 'MED', label: '📷 الأرشيف البصري والإعلامي',   folderId: 'f4',
        types: ['الصور التاريخية','الفيديو والتسجيلات','الخرائط والمصورات'] },
    ]
  },
  {
    id: 'PUB', label: '📚 المنشورات والأبحاث', folderId: 'f3',
    subjects: [
      { id: 'JRN', label: '📰 محتوى مجلة الدارة',         folderId: 'f3',
        types: ['مقالات المجلة','أعداد المجلة','ملخصات الأبحاث'] },
      { id: 'RES', label: '🔬 الأبحاث والدراسات',          folderId: 'f3',
        types: ['أوراق بحثية','دراسات أكاديمية','تقارير بحثية','رسائل علمية'] },
      { id: 'REP', label: '📑 التقارير المؤسسية',           folderId: 'f3',
        types: ['التقارير السنوية','تقارير الإنجاز','الإحصاءات والأرقام'] },
    ]
  },
]

// Map subject → folder
const SUBJECT_TO_FOLDER = {}
TAXONOMY.forEach(domain => {
  domain.subjects.forEach(sub => {
    SUBJECT_TO_FOLDER[sub.id] = sub.folderId
  })
})

const FILE_TYPES = ['تقرير','عقد','سياسة','محضر اجتماع','خطة','مذكرة','مرسوم','لائحة','دراسة','أخرى']
const CLASSIFICATIONS = ['عام','داخلي','سري','سري للغاية']
const FILE_ICON = { PDF:'📕', DOCX:'📘', XLSX:'📗', PPTX:'📙', ZIP:'📦', DOC:'📘', XLS:'📗' }
const STEPS = ['الملف','التصنيف','التفاصيل']

export function UploadModal({ onClose, onSuccess }) {
  const [step, setStep]     = useState(0)
  const [file, setFile]     = useState(null)
  const [drag, setDrag]     = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError]   = useState('')
  const inputRef = useRef()

  // Step 2: Taxonomy
  const [domain, setDomain]       = useState(null)
  const [subject, setSubject]     = useState(null)
  const [docType, setDocType]     = useState('')

  // Step 3: Details
  const [form, setForm] = useState({
    titleAr: '', titleEn: '',
    classification: 'داخلي',
    department: '',
    summary: '',
    tags: '',
    secondarySubjects: [],
  })

  const handleFile = (f) => {
    if (!f) return
    setFile(f)
    const ext = f.name.split('.').pop().toUpperCase()
    if (!form.titleAr) setForm(p => ({...p, titleAr: f.name.replace(/\.[^.]+$/, '')}))
    setStep(1)
  }

  const handleDrop = (e) => {
    e.preventDefault(); setDrag(false)
    handleFile(e.dataTransfer.files[0])
  }

  const toggleSecondary = (id) => {
    setForm(p => ({
      ...p,
      secondarySubjects: p.secondarySubjects.includes(id)
        ? p.secondarySubjects.filter(x => x !== id)
        : [...p.secondarySubjects, id]
    }))
  }

  const handleSubmit = async () => {
    if (!form.titleAr.trim()) { setError('العنوان مطلوب'); return }
    if (!domain)  { setError('يجب تحديد النطاق الرئيسي'); return }
    if (!subject) { setError('يجب تحديد الموضوع الرئيسي'); return }

    setLoading(true); setError('')
    const folderId = SUBJECT_TO_FOLDER[subject.id] || domain.folderId

    try {
      const fd = new FormData()
      if (file) fd.append('file', file)
      fd.append('titleAr', form.titleAr)
      fd.append('titleEn', form.titleEn)
      fd.append('documentType', docType || FILE_TYPES[0])
      fd.append('classification', form.classification)
      fd.append('summary', form.summary)
      fd.append('primarySubject', subject.id)
      fd.append('domain', domain.id)
      fd.append('folderId', folderId)
      await client.post('/api/v1/documents', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
    } catch {}

    const ext = file?.name.split('.').pop().toUpperCase() || 'PDF'
    const newDoc = {
      id: 'DOC-' + Date.now(),
      titleAr: form.titleAr, title: form.titleAr,
      titleEn: form.titleEn,
      type: docType || FILE_TYPES[0],
      classification: form.classification,
      version: '1.0', status: 'Draft',
      owner: 'المستخدم الحالي',
      department: form.department,
      summary: form.summary,
      tags: form.tags ? form.tags.split(',').map(t => t.trim()).filter(Boolean) : [],
      fileType: ext,
      fileSize: file ? (file.size/1024/1024).toFixed(1)+' MB' : '—',
      folder: folderId,
      folderName: domain.label,
      primarySubject: subject.label,
      domain: domain.label,
      pages: 1,
      attachments: [],
      history: [{ version:'1.0', date: new Date().toISOString().split('T')[0], by:'أنت', action:'رفع جديد' }],
      createdAt: new Date().toISOString().split('T')[0],
      updatedAt: new Date().toISOString().split('T')[0],
      thumb: FILE_ICON[ext] || '📄',
    }

    setLoading(false)
    // Save to shared library store so it appears in Library page
    addToLibrary(newDoc)
    onSuccess?.({ msg: `✅ تم رفع "${form.titleAr}" في ${subject.label}`, doc: newDoc })
    onClose()
  }

  const canGoNext = [
    !!file,
    !!(domain && subject),
    !!(form.titleAr.trim()),
  ]

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-xl max-h-[92vh] overflow-y-auto" onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="sticky top-0 bg-white p-4 border-b border-gray-100 z-10">
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-bold text-gray-900">رفع وثيقة جديدة</h2>
            <button onClick={onClose} className="text-gray-300 hover:text-gray-600 w-8 h-8 flex items-center justify-center rounded-lg hover:bg-gray-100 text-xl">✕</button>
          </div>
          {/* Progress steps */}
          <div className="flex items-center gap-1">
            {STEPS.map((s, i) => (
              <React.Fragment key={i}>
                <div className={`flex items-center gap-1.5 text-xs font-medium transition-colors ${
                  i < step ? 'text-green-600' : i === step ? 'text-blue-700' : 'text-gray-400'
                }`}>
                  <div className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold transition-colors ${
                    i < step ? 'bg-green-500 text-white' : i === step ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-400'
                  }`}>
                    {i < step ? '✓' : i + 1}
                  </div>
                  <span className="hidden sm:block">{s}</span>
                </div>
                {i < STEPS.length - 1 && (
                  <div className={`flex-1 h-px mx-1 ${i < step ? 'bg-green-300' : 'bg-gray-100'}`}/>
                )}
              </React.Fragment>
            ))}
          </div>
        </div>

        <div className="p-5">

          {/* ══ STEP 0: File ══ */}
          {step === 0 && (
            <div>
              <div
                onDrop={handleDrop}
                onDragOver={e => { e.preventDefault(); setDrag(true) }}
                onDragLeave={() => setDrag(false)}
                onClick={() => inputRef.current?.click()}
                className={`border-2 border-dashed rounded-2xl p-10 text-center cursor-pointer transition-all ${
                  drag ? 'border-blue-400 bg-blue-50' : 'border-gray-200 hover:border-blue-300 hover:bg-gray-50'
                }`}>
                <input ref={inputRef} type="file"
                  accept=".pdf,.docx,.xlsx,.pptx,.doc,.xls,.zip,.jpg,.png"
                  className="hidden"
                  onChange={e => handleFile(e.target.files[0])} />
                <div className="text-5xl mb-3">📁</div>
                <p className="font-semibold text-gray-700">اسحب وأفلت الملف هنا</p>
                <p className="text-sm text-gray-400 mt-1">أو اضغط للاختيار</p>
                <p className="text-xs text-gray-300 mt-2">PDF, DOCX, XLSX, PPTX, ZIP — حتى 50MB</p>
              </div>
            </div>
          )}

          {/* ══ STEP 1: Taxonomy ══ */}
          {step === 1 && (
            <div className="space-y-4">
              {file && (
                <div className="flex items-center gap-3 p-3 bg-green-50 border border-green-200 rounded-xl">
                  <span className="text-2xl">{FILE_ICON[file.name.split('.').pop().toUpperCase()] || '📄'}</span>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-800 truncate">{file.name}</p>
                    <p className="text-xs text-gray-400">{(file.size/1024/1024).toFixed(2)} MB</p>
                  </div>
                  <span className="text-green-500 text-lg">✓</span>
                </div>
              )}

              {/* Domain */}
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2">
                  النطاق الرئيسي <span className="text-red-400">*</span>
                </label>
                <div className="grid grid-cols-1 gap-2">
                  {TAXONOMY.map(d => (
                    <button key={d.id} onClick={() => { setDomain(d); setSubject(null); setDocType('') }}
                      className={`flex items-center gap-3 p-3 rounded-xl border-2 text-right transition-all ${
                        domain?.id === d.id
                          ? 'border-blue-500 bg-blue-50'
                          : 'border-gray-100 hover:border-blue-200 hover:bg-gray-50'
                      }`}>
                      <span className="text-xl">{d.label.split(' ')[0]}</span>
                      <div className="flex-1">
                        <p className={`text-sm font-semibold ${domain?.id===d.id?'text-blue-700':'text-gray-700'}`}>
                          {d.label.slice(2)}
                        </p>
                        <p className="text-xs text-gray-400">{d.subjects.length} موضوع</p>
                      </div>
                      {domain?.id === d.id && <span className="text-blue-500 text-lg">✓</span>}
                    </button>
                  ))}
                </div>
              </div>

              {/* Subject */}
              {domain && (
                <div>
                  <label className="block text-sm font-bold text-gray-700 mb-2">
                    الموضوع الرئيسي <span className="text-red-400">*</span>
                  </label>
                  <div className="grid grid-cols-1 gap-1.5 max-h-52 overflow-y-auto">
                    {domain.subjects.map(s => (
                      <button key={s.id} onClick={() => { setSubject(s); setDocType('') }}
                        className={`flex items-center gap-2.5 px-3 py-2.5 rounded-xl border text-right transition-all ${
                          subject?.id === s.id
                            ? 'border-blue-400 bg-blue-50 text-blue-700 font-semibold'
                            : 'border-gray-100 hover:border-blue-200 hover:bg-gray-50 text-gray-700'
                        }`}>
                        <span>{s.label.split(' ')[0]}</span>
                        <span className="flex-1 text-sm">{s.label.slice(2)}</span>
                        {subject?.id === s.id && <span className="text-blue-500 text-sm">✓</span>}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Doc type */}
              {subject && (
                <div>
                  <label className="block text-sm font-bold text-gray-700 mb-2">نوع الوثيقة</label>
                  <div className="flex flex-wrap gap-2">
                    {subject.types.map(t => (
                      <button key={t} onClick={() => setDocType(t)}
                        className={`text-xs px-3 py-1.5 rounded-full border transition-all font-medium ${
                          docType === t
                            ? 'bg-blue-600 text-white border-blue-600'
                            : 'border-gray-200 text-gray-600 hover:border-blue-300 hover:bg-blue-50'
                        }`}>
                        {t}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Secondary subjects */}
              {domain && subject && (
                <div>
                  <label className="block text-sm font-bold text-gray-700 mb-2">موضوعات فرعية إضافية (اختياري)</label>
                  <div className="flex flex-wrap gap-1.5">
                    {domain.subjects.filter(s => s.id !== subject.id).map(s => (
                      <button key={s.id} onClick={() => toggleSecondary(s.id)}
                        className={`text-xs px-2.5 py-1 rounded-full border transition-all ${
                          form.secondarySubjects.includes(s.id)
                            ? 'bg-purple-100 text-purple-700 border-purple-300'
                            : 'border-gray-200 text-gray-500 hover:border-purple-200 hover:bg-purple-50'
                        }`}>
                        {s.label.slice(2)}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Folder preview */}
              {subject && (
                <div className="flex items-center gap-2 p-3 bg-amber-50 border border-amber-200 rounded-xl">
                  <span className="text-lg">📁</span>
                  <div>
                    <p className="text-xs font-bold text-amber-800">سيُحفظ في المجلد:</p>
                    <p className="text-sm text-amber-700 font-semibold">{domain.label.slice(2)} ← {subject.label.slice(2)}</p>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* ══ STEP 2: Details ══ */}
          {step === 2 && (
            <div className="space-y-4">
              {/* Summary card */}
              {subject && (
                <div className="p-3 bg-blue-50 border border-blue-200 rounded-xl flex items-center gap-3">
                  <div>
                    <p className="text-xs font-bold text-blue-800">📂 {domain?.label.slice(2)}</p>
                    <p className="text-xs text-blue-600">🏷️ {subject?.label.slice(2)}{docType ? ` → ${docType}` : ''}</p>
                  </div>
                </div>
              )}

              <div>
                <label className="block text-xs font-bold text-gray-700 mb-1.5">العنوان بالعربية <span className="text-red-400">*</span></label>
                <input value={form.titleAr} onChange={e=>setForm(p=>({...p,titleAr:e.target.value}))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
                  placeholder="عنوان الوثيقة بالعربية..." />
              </div>

              <div>
                <label className="block text-xs font-bold text-gray-700 mb-1.5">العنوان بالإنجليزية</label>
                <input value={form.titleEn} onChange={e=>setForm(p=>({...p,titleEn:e.target.value}))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
                  placeholder="Document title in English..." dir="ltr"/>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1.5">التصنيف الأمني</label>
                  <div className="flex flex-col gap-1">
                    {CLASSIFICATIONS.map(c => (
                      <button key={c} onClick={() => setForm(p=>({...p,classification:c}))}
                        className={`text-xs px-2.5 py-1.5 rounded-lg border text-right transition-all font-medium ${
                          form.classification===c
                            ? 'border-blue-400 bg-blue-50 text-blue-700'
                            : 'border-gray-200 text-gray-600 hover:bg-gray-50'
                        }`}>
                        {c}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">الإدارة / القسم</label>
                    <input value={form.department} onChange={e=>setForm(p=>({...p,department:e.target.value}))}
                      className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
                      placeholder="الإدارة المسؤولة..." />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">الوسوم (tags)</label>
                    <input value={form.tags} onChange={e=>setForm(p=>({...p,tags:e.target.value}))}
                      className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
                      placeholder="وسم1، وسم2، ..." />
                  </div>
                </div>
              </div>

              <div>
                <label className="block text-xs font-bold text-gray-700 mb-1.5">الملخص</label>
                <textarea value={form.summary} onChange={e=>setForm(p=>({...p,summary:e.target.value}))}
                  rows={3} className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"
                  placeholder="وصف مختصر للوثيقة..." />
              </div>

              {error && <p className="text-red-500 text-xs bg-red-50 border border-red-200 px-3 py-2 rounded-lg">{error}</p>}
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
            <button onClick={() => { if (canGoNext[step]) setStep(s => s + 1) }}
              disabled={!canGoNext[step]}
              className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
              التالي →
            </button>
          ) : (
            <button onClick={handleSubmit} disabled={loading}
              className="flex-1 bg-green-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-green-700 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors">
              {loading ? '⏳ جارٍ الرفع...' : '📤 رفع الوثيقة'}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
