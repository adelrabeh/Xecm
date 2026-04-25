import { useAuthStore } from '../../store/authStore'
import { useLibraryFilesV2 } from '../../hooks/useFolderStore'
import { useLocalStorage } from '../../hooks/useLocalStorage'
import React, { useState, useEffect } from 'react'
import { useLang } from '../../i18n.js'
import client from '../../api/client'
import { UploadModal } from '../../components/UploadModal'
import { PreviewModal } from '../../components/PreviewModal'
import { ShareModal } from '../../components/ShareModal'
import { useToast } from '../../components/Toast'
import { useLocalStorage as useLS } from '../../hooks/useLocalStorage'

const MOCK_DOCS = [
  { id:'DOC-2026-001', titleAr:'تقرير الميزانية السنوي 2026', titleEn:'Annual Budget Report 2026', status:'Approved', type:'تقرير مالي', classification:'سري', version:'3.1', createdAt:'2026-04-01', updatedAt:'2026-04-10', expiryDate:'2027-04-01', owner:'أحمد الزهراني', department:'الشؤون المالية', fileSize:'2.4 MB', fileType:'PDF', pages:48, language:'العربية', summary:'يتضمن الميزانية السنوية لعام 2026 مع تفصيل البنود والاعتمادات المالية لجميع الإدارات.', tags:['مالي','ميزانية','2026'], attachments:[{name:'الميزانية التفصيلية.xlsx',size:'1.1 MB',type:'XLSX'},{name:'مرفق الاعتمادات.pdf',size:'0.8 MB',type:'PDF'}], history:[{version:'3.1',date:'2026-04-10',by:'أحمد الزهراني',action:'اعتماد نهائي'},{version:'3.0',date:'2026-04-05',by:'سارة العتيبي',action:'مراجعة'},{version:'2.0',date:'2026-03-20',by:'أحمد الزهراني',action:'تحديث'}] },
  { id:'DOC-2026-002', titleAr:'عقد توريد المستلزمات المكتبية', titleEn:'Office Supplies Contract', status:'UnderReview', type:'عقد', classification:'داخلي', version:'2.0', createdAt:'2026-03-28', updatedAt:'2026-04-02', expiryDate:'2027-03-28', owner:'مريم العنزي', department:'الشؤون الإدارية', fileSize:'1.2 MB', fileType:'PDF', pages:22, language:'العربية', summary:'عقد توريد المستلزمات والقرطاسية المكتبية للفترة 2026-2027.', tags:['عقود','توريد'], attachments:[{name:'قائمة الأسعار.xlsx',size:'0.5 MB',type:'XLSX'}], history:[{version:'2.0',date:'2026-04-02',by:'مريم العنزي',action:'تعديل'},{version:'1.0',date:'2026-03-28',by:'مريم العنزي',action:'إنشاء'}] },
  { id:'DOC-2026-003', titleAr:'سياسة حماية البيانات المحدثة', titleEn:'Data Protection Policy', status:'Active', type:'سياسة', classification:'عام', version:'4.0', createdAt:'2026-03-25', updatedAt:'2026-03-25', expiryDate:'2028-03-25', owner:'خالد القحطاني', department:'تقنية المعلومات', fileSize:'0.9 MB', fileType:'PDF', pages:35, language:'العربية / الإنجليزية', summary:'السياسة المحدثة لحماية البيانات وفق متطلبات هيئة حماية البيانات الشخصية.', tags:['سياسات','بيانات','امتثال'], attachments:[], history:[{version:'4.0',date:'2026-03-25',by:'خالد القحطاني',action:'إصدار رسمي'},{version:'3.5',date:'2026-02-10',by:'خالد القحطاني',action:'مسودة'}] },
  { id:'DOC-2026-004', titleAr:'محضر اجتماع مجلس الإدارة Q1', titleEn:'Board Meeting Minutes Q1', status:'Active', type:'محضر اجتماع', classification:'سري للغاية', version:'1.0', createdAt:'2026-03-20', updatedAt:'2026-03-22', expiryDate:null, owner:'فاطمة الشمري', department:'الرئاسة التنفيذية', fileSize:'3.1 MB', fileType:'PDF', pages:15, language:'العربية', summary:'محضر اجتماع مجلس الإدارة للربع الأول 2026 يتضمن قرارات المجلس وخطة العمل.', tags:['مجلس الإدارة','محاضر','Q1'], attachments:[{name:'عروض Q1.pptx',size:'5.2 MB',type:'PPTX'},{name:'تقرير الأداء.pdf',size:'1.8 MB',type:'PDF'}], history:[{version:'1.0',date:'2026-03-22',by:'فاطمة الشمري',action:'اعتماد'},{version:'0.9',date:'2026-03-20',by:'فاطمة الشمري',action:'إنشاء'}] },
  { id:'DOC-2025-089', titleAr:'تقرير التدقيق الداخلي 2025', titleEn:'Internal Audit Report 2025', status:'Archived', type:'تقرير تدقيق', classification:'سري', version:'2.0', createdAt:'2026-01-15', updatedAt:'2026-01-20', expiryDate:'2031-01-15', owner:'عمر الدوسري', department:'التدقيق الداخلي', fileSize:'4.7 MB', fileType:'PDF', pages:120, language:'العربية', summary:'التقرير السنوي للتدقيق الداخلي يغطي تقييم ضوابط الرقابة وإجراءات الحوكمة.', tags:['تدقيق','2025','حوكمة'], attachments:[{name:'ملاحق التدقيق.pdf',size:'2.1 MB',type:'PDF'}], history:[{version:'2.0',date:'2026-01-20',by:'عمر الدوسري',action:'أرشفة'},{version:'1.0',date:'2026-01-15',by:'عمر الدوسري',action:'إصدار'}] },
  { id:'DOC-2026-006', titleAr:'خطة الاستمرارية التشغيلية', titleEn:'Business Continuity Plan', status:'Draft', type:'خطة', classification:'داخلي', version:'0.3', createdAt:'2026-04-10', updatedAt:'2026-04-15', expiryDate:null, owner:'نورة السبيعي', department:'إدارة المخاطر', fileSize:'1.8 MB', fileType:'DOCX', pages:60, language:'العربية', summary:'مسودة خطة الاستمرارية التشغيلية لضمان استمرار العمل في حالات الطوارئ.', tags:['مخاطر','طوارئ','استمرارية'], attachments:[], history:[{version:'0.3',date:'2026-04-15',by:'نورة السبيعي',action:'تحديث مسودة'},{version:'0.1',date:'2026-04-10',by:'نورة السبيعي',action:'إنشاء'}] },
]

const STATUS_MAP = {
  Active:      { label:'نشط',          cls:'bg-green-100 text-green-700',   dot:'bg-green-500' },
  UnderReview: { label:'قيد المراجعة', cls:'bg-yellow-100 text-yellow-700', dot:'bg-yellow-500' },
  Approved:    { label:'معتمد',         cls:'bg-blue-100 text-blue-700',     dot:'bg-blue-500' },
  Archived:    { label:'مؤرشف',         cls:'bg-gray-100 text-gray-500',     dot:'bg-gray-400' },
  Draft:       { label:'مسودة',         cls:'bg-purple-100 text-purple-700', dot:'bg-purple-500' },
}
const CLASS_MAP = {
  'عام':         { cls:'bg-green-50 text-green-600 border border-green-200' },
  'داخلي':      { cls:'bg-blue-50 text-blue-600 border border-blue-200' },
  'سري':         { cls:'bg-orange-50 text-orange-600 border border-orange-200' },
  'سري للغاية': { cls:'bg-red-50 text-red-600 border border-red-200' },
}
const FILE_ICON = { PDF:'📕', DOCX:'📘', XLSX:'📗', PPTX:'📙' }

function Row({ label, value, mono=false }) {
  if (value===null||value===undefined) return null
  return (
    <div className="flex justify-between items-start py-2 border-b border-gray-50 last:border-0">
      <span className="text-gray-400 text-xs w-28 flex-shrink-0">{label}</span>
      <span className={`text-xs font-medium text-gray-800 text-right ${mono?'font-mono':''}`}>{value}</span>
    </div>
  )
}

function ConfirmDialog({ message, onConfirm, onCancel }) {
  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50" onClick={onCancel}>
      <div className="bg-white rounded-2xl p-6 shadow-2xl max-w-sm w-full mx-4" onClick={e=>e.stopPropagation()}>
        <p className="text-gray-800 text-sm text-center mb-5">{message}</p>
        <div className="flex gap-3">
          <button onClick={onConfirm} className="flex-1 bg-red-600 text-white py-2 rounded-xl text-sm hover:bg-red-700">تأكيد</button>
          <button onClick={onCancel} className="flex-1 border border-gray-200 text-gray-600 py-2 rounded-xl text-sm hover:bg-gray-50">إلغاء</button>
        </div>
      </div>
    </div>
  )
}

export default function DocumentsPage() {
  const { lang, setLang, t, isRTL, fmtDate, fmtNum } = useLang()
  const { user }              = useAuthStore()
  const isAdmin               = (user?.permissions||[]).some(p => p==='admin.*'||p==='documents.all')
  const currentUser           = user?.fullNameAr || user?.username || 'أنت'
  const [docs, setDocs]       = useLocalStorage('ecm_docs', MOCK_DOCS)
  const [sharedWithMe]        = useLocalStorage('ecm_shared_'+user?.username, [])
  const [libraryFilesRaw]     = useLibraryFilesV2()
  const libraryUploads        = Array.isArray(libraryFilesRaw) ? libraryFilesRaw : []
  const [search, setSearch]   = useState('')
  const [filter, setFilter]   = useState('all')
  const [loading, setLoading] = useState(false)
  const [sel, setSel]         = useState(null)
  const [tab, setTab]         = useState('details')
  const [showUpload, setShowUpload] = useState(false)
  const [confirm, setConfirm] = useState(null)
  const [previewDoc, setPreviewDoc] = useState(null)
  const [shareDoc, setShareDoc]     = useState(null)
  const { show, ToastContainer } = useToast()

  useEffect(() => {
    setLoading(true)
    client.get('/api/v1/documents', { params: { page:1, pageSize:50 } })
      .then(res => {
        const d = res.data?.data?.items || res.data?.data || res.data?.items
        if (Array.isArray(d) && d.length > 0) setDocs(d)
      })
      .catch(()=>{})
      .finally(()=>setLoading(false))
  }, [])

  const open = (doc) => { setSel(doc); setTab('details') }

  // Merge all sources
  const allDocs = [
    ...libraryUploads.filter(u => !docs.find(d => d.id === u.id)),
    ...docs
  ]

  // Permission filter: admin sees all, others see only their files + shared with them
  const visibleDocs = isAdmin
    ? allDocs
    : allDocs.filter(d => {
        const isOwner   = (d.owner === currentUser) || (d.owner === 'أنت') || !d.owner
        const isShared  = (Array.isArray(sharedWithMe) ? sharedWithMe : []).some(s => s.docId === d.id)
        return isOwner || isShared
      })

  const filtered = visibleDocs.filter(d => {
    const t = (d.titleAr||d.title||d.name||'').toLowerCase() + (d.id||'').toLowerCase()
    return t.includes(search.toLowerCase()) && (filter==='all'||d.status===filter)
  })

  // ── Button handlers ──────────────────────────────────────────────────────────

  const handleDownload = (doc) => {
    // Create a fake download link for demo
    const a = document.createElement('a')
    a.href = `/api/v1/documents/${doc.documentId || doc.id}/download`
    a.download = (doc.titleAr||doc.title) + '.' + (doc.fileType||'pdf').toLowerCase()
    show(`جارٍ تنزيل "${doc.titleAr||doc.title}"`, 'info')
    // In real scenario: a.click()
  }

  const handleShare = (doc) => {
    const url = `${window.location.origin}/documents/${doc.id}`
    navigator.clipboard?.writeText(url).then(() => {
      show('تم نسخ رابط الوثيقة', 'success')
    }).catch(() => show('رابط الوثيقة: ' + url, 'info'))
  }

  const handleEdit = (doc) => {
    show('فتح وضع التعديل للوثيقة: ' + (doc.titleAr||doc.title), 'info')
  }

  const handleCheckout = (doc) => {
    client.post(`/api/v1/documents/${doc.documentId||doc.id}/checkout`)
      .then(() => show('تم استعارة الوثيقة بنجاح، يمكنك الآن تعديلها', 'success'))
      .catch(() => show('تم تسجيل الاستعارة (وضع العرض)', 'success'))
  }

  const handleSendApproval = (doc) => {
    client.post(`/api/v1/workflow/submit/${doc.documentId||doc.id}`, { priority: 2 })
      .then(() => show('تم إرسال الوثيقة لسير الاعتماد بنجاح', 'success'))
      .catch(() => show('تم إرسال طلب الاعتماد (وضع العرض)', 'success'))
  }

  const handlePrint = (doc) => {
    show(`جارٍ تجهيز الطباعة: "${doc.titleAr||doc.title}"`, 'info')
    setTimeout(() => window.print(), 500)
  }

  const handleArchive = (doc) => {
    setConfirm({
      message: `هل تريد أرشفة "${doc.titleAr||doc.title}"؟ لن يمكن التراجع عن هذا الإجراء.`,
      onConfirm: async () => {
        setConfirm(null)
        try {
          await client.post(`/api/v1/documents/${doc.documentId||doc.id}/archive`)
          setDocs(prev => prev.map(d => d.id===doc.id ? {...d, status:'Archived'} : d))
          if (sel?.id===doc.id) setSel({...doc, status:'Archived'})
          show('تم أرشفة الوثيقة بنجاح', 'success')
        } catch {
          setDocs(prev => prev.map(d => d.id===doc.id ? {...d, status:'Archived'} : d))
          show('تم أرشفة الوثيقة (وضع العرض)', 'success')
        }
      }
    })
  }

  const handleAttachmentDownload = (att) => {
    show(`جارٍ تنزيل "${att.name}"`, 'info')
  }

  const handleAddAttachment = () => {
    const input = document.createElement('input')
    input.type = 'file'
    input.onchange = e => {
      const f = e.target.files[0]
      if (f) show(`تم إضافة المرفق: ${f.name}`, 'success')
    }
    input.click()
  }

  const handleImport = () => {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.pdf,.docx,.xlsx,.zip,.csv'
    input.onchange = e => {
      const f = e.target.files[0]
      if (f) {
        show(`جارٍ استيراد: ${f.name}`, 'info')
        setTimeout(() => show(`تم استيراد "${f.name}" بنجاح`, 'success'), 1500)
      }
    }
    input.click()
  }

  const ACTIONS = [
    { label:'🔗 مشاركة',           fn: (doc)=>setShareDoc(doc), cls:'text-purple-600 border-purple-200 hover:bg-purple-50' },
    { label:'🔒 استعارة',          fn: handleCheckout,     cls:'text-gray-600 border-gray-200 hover:bg-gray-50' },
    { label:'📤 إرسال للاعتماد',  fn: handleSendApproval, cls:'text-blue-600 border-blue-200 hover:bg-blue-50' },
    { label:'👁️ معاينة',          fn: (doc)=>setPreviewDoc(doc), cls:'text-blue-600 border-blue-200 hover:bg-blue-50' },
    { label:'🖨️ طباعة',           fn: handlePrint,        cls:'text-gray-600 border-gray-200 hover:bg-gray-50' },
    { label:'🗃️ أرشفة',           fn: handleArchive,      cls:'text-red-500 border-red-100 hover:bg-red-50' },
  ]

  return (
    <div className="flex h-full gap-3">
      <ToastContainer />
      {showUpload && <UploadModal onClose={()=>setShowUpload(false)} onSuccess={({msg,doc})=>{show(msg,'success');setShowUpload(false);if(doc)setDocs(prev=>[doc,...prev])}} />}
      {confirm && <ConfirmDialog {...confirm} onCancel={()=>setConfirm(null)} />}
      {previewDoc && <PreviewModal file={previewDoc} onClose={()=>setPreviewDoc(null)} />}
      {shareDoc && <ShareModal file={shareDoc} onClose={()=>setShareDoc(null)} />}

      {/* ── List ── */}
      <div className={`flex flex-col gap-3 transition-all ${sel?'w-[52%]':'w-full'}`}>
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-lg font-bold text-gray-900">t('docs_title')</h1>
            <p className="text-gray-400 text-xs">{filtered.length} {isAdmin ? "وثيقة (عرض المدير)" : "ملف خاص بك"}</p>
          </div>
          <div className="flex gap-2">
            <button onClick={handleImport}
              className="border border-gray-200 text-gray-500 text-xs px-3 py-1.5 rounded-lg hover:bg-gray-50 transition-colors flex items-center gap-1.5">
              📥 استيراد
            </button>
            <button onClick={()=>setShowUpload(true)}
              className="bg-blue-700 text-white text-xs px-4 py-1.5 rounded-lg hover:bg-blue-800 transition-colors flex items-center gap-1.5 shadow-sm">
              ＋ t('upload_doc')
            </button>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-3 flex gap-2">
          <div className="relative flex-1">
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
            <input type="text" placeholder="البحث..." value={search} onChange={e=>setSearch(e.target.value)}
              className="w-full pr-9 pl-3 py-2 border border-gray-200 rounded-lg text-xs focus:outline-none focus:ring-2 focus:ring-blue-400 text-right" />
          </div>
          <select value={filter} onChange={e=>setFilter(e.target.value)}
            className="border border-gray-200 rounded-lg px-3 text-xs text-gray-600 focus:outline-none">
            <option value="all">كل الحالات</option>
            {Object.entries(STATUS_MAP).map(([k,v])=><option key={k} value={k}>{v.label}</option>)}
          </select>
        </div>

        <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden flex-1">
          <div className="overflow-auto h-full">
            <table className="w-full text-xs">
              <thead className="sticky top-0 bg-gray-50 border-b border-gray-100 z-10">
                <tr>
                  <th className="px-3 py-2.5 text-right font-semibold text-gray-400">الوثيقة</th>
                  <th className="px-3 py-2.5 text-right font-semibold text-gray-400">الحالة</th>
                  <th className="px-3 py-2.5 text-right font-semibold text-gray-400 hidden md:table-cell">التصنيف</th>
                  <th className="px-3 py-2.5 text-right font-semibold text-gray-400">الإصدار</th>
                  <th className="px-3 py-2.5 text-right font-semibold text-gray-400 hidden lg:table-cell">آخر تعديل</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {loading
                  ? Array.from({length:5}).map((_,i)=><tr key={i}>{Array.from({length:5}).map((_,j)=><td key={j} className="px-3 py-3"><div className="h-3 bg-gray-100 rounded animate-pulse"/></td>)}</tr>)
                  : filtered.length===0
                    ? <tr><td colSpan={5} className="text-center py-16 text-gray-300"><div className="text-3xl mb-2">📭</div><p>لا توجد وثائق</p></td></tr>
                    : filtered.map(doc=>{
                        const s=STATUS_MAP[doc.status]||{label:doc.status,cls:'bg-gray-100 text-gray-500',dot:'bg-gray-400'}
                        const c=CLASS_MAP[doc.classification]||{cls:'bg-gray-50 text-gray-500 border border-gray-200'}
                        return (
                          <tr key={doc.id} onClick={()=>sel?.id===doc.id?setSel(null):open(doc)}
                            className={`cursor-pointer transition-colors ${sel?.id===doc.id?'bg-blue-50':'hover:bg-gray-50'}`}>
                            <td className="px-3 py-2.5">
                              <div className="flex items-center gap-2">
                                <span className="text-lg">{FILE_ICON[doc.fileType]||'📄'}</span>
                                <div>
                                  <p className="font-semibold text-gray-800 truncate max-w-[160px]">{doc.titleAr||doc.name||doc.title}</p>
                                  <p className="text-gray-400 text-[10px]">{doc.fileType||doc.type||'وثيقة'} • {doc.fileSize||doc.size||''}</p>
                                </div>
                              </div>
                            </td>
                            <td className="px-3 py-2.5">
                              <span className={`inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-full font-medium ${s.cls}`}>
                                <span className={`w-1.5 h-1.5 rounded-full flex-shrink-0 ${s.dot}`}/>
                                {s.label}
                              </span>
                            </td>
                            <td className="px-3 py-2.5 hidden md:table-cell">
                              <span className={`text-[10px] px-1.5 py-0.5 rounded font-medium ${c.cls}`}>{doc.classification||'—'}</span>
                            </td>
                            <td className="px-3 py-2.5 text-gray-500 font-mono">{doc.version||'1.0'}</td>
                            <td className="px-3 py-2.5 text-gray-400 hidden lg:table-cell">
                              {doc.updatedAt?new Date(doc.updatedAt).toLocaleDateString('ar-SA'):'—'}
                            </td>
                          </tr>
                        )
                      })
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* ── Detail Panel ── */}
      {sel && (
        <div className="w-[48%] bg-white rounded-xl border border-gray-100 shadow-sm flex flex-col overflow-hidden">
          {/* Header */}
          <div className="p-4 border-b border-gray-100 flex items-start gap-3">
            <div className="w-10 h-10 bg-blue-50 rounded-xl flex items-center justify-center text-xl flex-shrink-0">
              {FILE_ICON[sel.fileType]||'📄'}
            </div>
            <div className="flex-1 min-w-0">
              <h2 className="font-bold text-gray-900 text-sm leading-snug">{sel.titleAr}</h2>
              {sel.titleEn && <p className="text-gray-400 text-[11px] mt-0.5">{sel.titleEn}</p>}
              <p className="text-gray-300 text-[10px] font-mono mt-1">{sel.id}</p>
            </div>
            <button onClick={()=>setSel(null)} className="text-gray-300 hover:text-gray-500 text-xl">✕</button>
          </div>

          {/* Status bar */}
          <div className="px-4 py-2 bg-gray-50/60 border-b border-gray-100 flex items-center justify-between">
            <div className="flex items-center gap-2">
              {(()=>{const s=STATUS_MAP[sel.status]||{};return(
                <span className={`inline-flex items-center gap-1 text-[11px] px-2.5 py-1 rounded-full font-medium ${s.cls}`}>
                  <span className={`w-1.5 h-1.5 rounded-full ${s.dot}`}/>{s.label}
                </span>
              )})()}
              {sel.classification&&<span className={`text-[11px] px-2 py-0.5 rounded font-medium ${(CLASS_MAP[sel.classification]||{}).cls}`}>{sel.classification}</span>}
            </div>
            <div className="flex gap-1">
              <button onClick={()=>setPreviewDoc(sel)} className="bg-blue-700 text-white text-[11px] px-3 py-1.5 rounded-lg hover:bg-blue-800 transition-colors">👁 معاينة</button>
              <button onClick={()=>setShareDoc(sel)} className="border border-gray-200 text-gray-500 text-[11px] px-2.5 py-1.5 rounded-lg hover:bg-gray-50 transition-colors" title="مشاركة">🔗</button>
              <button onClick={()=>handleEdit(sel)} className="border border-gray-200 text-gray-500 text-[11px] px-2.5 py-1.5 rounded-lg hover:bg-gray-50 transition-colors" title="تعديل">✏️</button>
            </div>
          </div>

          {/* Tabs */}
          <div className="flex border-b border-gray-100">
            {[{key:'details',label:'التفاصيل'},{key:'attachments',label:`المرفقات${sel.attachments?.length?` (${sel.attachments.length})`:''}`},{key:'history',label:'السجل'}].map(t=>(
              <button key={t.key} onClick={()=>setTab(t.key)}
                className={`px-4 py-2.5 text-xs font-medium border-b-2 transition-colors ${tab===t.key?'border-blue-600 text-blue-700':'border-transparent text-gray-400 hover:text-gray-600'}`}>
                {t.label}
              </button>
            ))}
          </div>

          {/* Content */}
          <div className="flex-1 overflow-y-auto p-4 space-y-4">
            {tab==='details' && <>
              {sel.summary&&<div className="bg-blue-50 rounded-xl p-3.5"><p className="text-[11px] font-semibold text-blue-600 mb-1.5">📝 الملخص</p><p className="text-xs text-gray-700 leading-relaxed">{sel.summary}</p></div>}
              {sel.tags?.length>0&&<div className="flex flex-wrap gap-1.5">{sel.tags.map(tag=><span key={tag} className="bg-gray-100 text-gray-500 text-[11px] px-2.5 py-0.5 rounded-full">#{tag}</span>)}</div>}
              <div className="bg-gray-50 rounded-xl p-4">
                <p className="text-[11px] font-semibold text-gray-400 uppercase mb-3">معلومات الوثيقة</p>
                <Row label="رقم الوثيقة"   value={sel.id} mono/>
                <Row label="نوع الوثيقة"   value={sel.type}/>
                <Row label="رقم الإصدار"   value={sel.version} mono/>
                <Row label="المالك"         value={sel.owner}/>
                <Row label="الإدارة"        value={sel.department}/>
                <Row label="اللغة"          value={sel.language}/>
                <Row label="عدد الصفحات"   value={sel.pages?`${sel.pages} صفحة`:undefined}/>
                <Row label="حجم الملف"     value={sel.fileSize}/>
                <Row label="نوع الملف"     value={sel.fileType}/>
              </div>
              <div className="bg-gray-50 rounded-xl p-4">
                <p className="text-[11px] font-semibold text-gray-400 uppercase mb-3">التواريخ</p>
                <Row label="تاريخ الإنشاء"  value={sel.createdAt?new Date(sel.createdAt).toLocaleDateString('ar-SA'):undefined}/>
                <Row label="آخر تعديل"      value={sel.updatedAt?new Date(sel.updatedAt).toLocaleDateString('ar-SA'):undefined}/>
                <Row label="تاريخ الانتهاء" value={sel.expiryDate?new Date(sel.expiryDate).toLocaleDateString('ar-SA'):'غير محدد'}/>
              </div>
              <div className="bg-gray-50 rounded-xl p-4">
                <p className="text-[11px] font-semibold text-gray-400 uppercase mb-3">إجراءات سير العمل</p>
                <div className="grid grid-cols-2 gap-2">
                  {ACTIONS.map(a=>(
                    <button key={a.label} onClick={()=>a.fn(sel)}
                      className={`bg-white border text-xs py-2 rounded-lg flex items-center justify-center gap-1.5 transition-colors ${a.cls}`}>
                      {a.label}
                    </button>
                  ))}
                </div>
              </div>
            </>}

            {tab==='attachments'&&(
              <div className="space-y-2">
                {!sel.attachments?.length
                  ? <div className="text-center py-12"><div className="text-3xl mb-2">📎</div><p className="text-xs text-gray-400">لا توجد مرفقات</p></div>
                  : sel.attachments.map((a,i)=>(
                    <div key={i} className="flex items-center justify-between p-3 bg-gray-50 rounded-xl hover:bg-gray-100 transition-colors">
                      <div className="flex items-center gap-3">
                        <span className="text-xl">{FILE_ICON[a.type]||'📄'}</span>
                        <div>
                          <p className="text-xs font-medium text-gray-800">{a.name}</p>
                          <p className="text-[10px] text-gray-400">{a.size} • {a.type}</p>
                        </div>
                      </div>
                      <button onClick={()=>handleAttachmentDownload(a)} className="text-blue-600 text-xs px-3 py-1.5 rounded-lg hover:bg-blue-50 transition-colors">⬇</button>
                    </div>
                  ))
                }
                <button onClick={handleAddAttachment}
                  className="w-full border border-dashed border-gray-300 text-gray-400 text-xs py-3 rounded-xl hover:border-blue-400 hover:text-blue-500 transition-colors mt-2">
                  + إضافة مرفق
                </button>
              </div>
            )}

            {tab==='history'&&(
              <div>
                {sel.history?.map((h,i)=>(
                  <div key={i} className="flex gap-3 pb-4">
                    <div className="flex flex-col items-center">
                      <div className={`w-7 h-7 rounded-full flex items-center justify-center text-[10px] text-white font-bold flex-shrink-0 ${i===0?'bg-blue-600':'bg-gray-300'}`}>{h.version}</div>
                      {i<sel.history.length-1&&<div className="w-px flex-1 bg-gray-100 my-1"/>}
                    </div>
                    <div className="flex-1 pb-2">
                      <div className="flex justify-between items-center mb-0.5">
                        <span className="text-xs font-semibold text-gray-800">{h.action}</span>
                        <span className={`text-[10px] px-2 py-0.5 rounded-full font-mono ${i===0?'bg-blue-50 text-blue-600':'bg-gray-50 text-gray-400'}`}>v{h.version}</span>
                      </div>
                      <p className="text-[11px] text-gray-500">{h.by}</p>
                      <p className="text-[10px] text-gray-300">{new Date(h.date).toLocaleDateString('ar-SA',{year:'numeric',month:'long',day:'numeric'})}</p>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
