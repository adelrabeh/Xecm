import { useLibraryFiles } from '../../hooks/useLibraryFiles'
import client from '../../api/client'
import { PreviewModal } from '../../components/PreviewModal'
import { ShareModal } from '../../components/ShareModal'
import React, { useState, useEffect } from 'react'
import { useToast } from '../../components/Toast'

const MOCK_FOLDERS = [
  { id:'f1', name:'الوثائق الإدارية',  parent:null, count:24 },
  { id:'f2', name:'العقود والاتفاقيات',parent:null, count:12 },
  { id:'f3', name:'التقارير السنوية',  parent:null, count:8 },
  { id:'f4', name:'الصور والوسائط',   parent:null, count:35 },
  { id:'f5', name:'لوائح وأنظمة',     parent:'f1', count:6 },
  { id:'f6', name:'مراسلات 2026',     parent:'f1', count:18 },
]

const MOCK_FILES = [
  { id:'d1', name:'تقرير الميزانية السنوي 2026.pdf', type:'PDF',  size:'2.4 MB', modified:'2026-04-10', created:'2026-01-15', version:'3.1', owner:'أحمد الزهراني', folder:'f1', tags:['مالي','2026'],   isFav:true,  isCheckedOut:false, likes:5,  comments:3,  thumb:'📕', classification:'سري',       status:'Approved' },
  { id:'d2', name:'عقد توريد المستلزمات 2026.docx', type:'DOCX', size:'1.2 MB', modified:'2026-04-08', created:'2026-02-10', version:'2.0', owner:'مريم العنزي',  folder:'f2', tags:['عقود'],           isFav:false, isCheckedOut:true,  likes:2,  comments:1,  thumb:'📘', classification:'داخلي',     status:'UnderReview' },
  { id:'d3', name:'سياسة حماية البيانات.pdf',       type:'PDF',  size:'0.9 MB', modified:'2026-03-25', created:'2025-11-01', version:'4.0', owner:'خالد القحطاني',folder:'f1', tags:['سياسات'],         isFav:true,  isCheckedOut:false, likes:8,  comments:5,  thumb:'📕', classification:'عام',       status:'Active' },
  { id:'d4', name:'خطة الاستمرارية التشغيلية.docx', type:'DOCX', size:'1.8 MB', modified:'2026-04-15', created:'2026-04-01', version:'0.3', owner:'نورة السبيعي', folder:'f3', tags:['مخاطر'],          isFav:false, isCheckedOut:false, likes:0,  comments:0,  thumb:'📘', classification:'داخلي',     status:'Draft' },
  { id:'d5', name:'محضر اجتماع مجلس الإدارة.pdf',  type:'PDF',  size:'3.1 MB', modified:'2026-03-22', created:'2026-03-20', version:'1.0', owner:'فاطمة الشمري', folder:'f1', tags:['محاضر','Q1'],     isFav:true,  isCheckedOut:false, likes:3,  comments:2,  thumb:'📕', classification:'سري للغاية', status:'Active' },
  { id:'d6', name:'الهوية البصرية للدارة.zip',      type:'ZIP',  size:'45 MB',  modified:'2026-02-14', created:'2025-09-01', version:'2.1', owner:'عمر الدوسري',  folder:'f4', tags:['تصميم'],          isFav:false, isCheckedOut:false, likes:12, comments:7,  thumb:'📦', classification:'عام',       status:'Active' },
  { id:'d7', name:'تقرير التدقيق الداخلي 2025.pdf', type:'PDF',  size:'4.7 MB', modified:'2026-01-20', created:'2025-12-01', version:'2.0', owner:'عمر الدوسري',  folder:'f3', tags:['تدقيق'],          isFav:false, isCheckedOut:false, likes:1,  comments:0,  thumb:'📕', classification:'سري',       status:'Archived' },
  { id:'d8', name:'عرض الرؤية الاستراتيجية.pptx',  type:'PPTX', size:'8.2 MB', modified:'2026-04-12', created:'2026-03-15', version:'1.5', owner:'أحمد الزهراني', folder:'f3', tags:['استراتيجي'],     isFav:true,  isCheckedOut:false, likes:15, comments:9,  thumb:'📙', classification:'سري',       status:'Active' },
  { id:'d9', name:'صور الفعاليات 2026.zip',         type:'ZIP',  size:'120 MB', modified:'2026-04-05', created:'2026-04-05', version:'1.0', owner:'مريم العنزي',  folder:'f4', tags:['صور','2026'],     isFav:false, isCheckedOut:false, likes:4,  comments:1,  thumb:'🖼', classification:'عام',       status:'Active' },
  { id:'d10',name:'دليل الموظف المحدث.pdf',         type:'PDF',  size:'2.1 MB', modified:'2026-03-01', created:'2025-06-01', version:'5.0', owner:'خالد القحطاني',folder:'f1', tags:['HR','دليل'],      isFav:false, isCheckedOut:false, likes:6,  comments:4,  thumb:'📕', classification:'داخلي',     status:'Active' },
]

const ALL_TAGS   = ['مالي','2026','عقود','سياسات','مخاطر','محاضر','Q1','تصميم','تدقيق','استراتيجي','HR','دليل']
const STATUS_CLS = { Active:'bg-green-100 text-green-700', Approved:'bg-blue-100 text-blue-700', Draft:'bg-purple-100 text-purple-700', Archived:'bg-gray-100 text-gray-500', UnderReview:'bg-yellow-100 text-yellow-700' }
const STATUS_LBL = { Active:'نشط', Approved:'معتمد', Draft:'مسودة', Archived:'مؤرشف', UnderReview:'قيد المراجعة' }
const CLASS_CLS  = { 'عام':'bg-green-50 text-green-600 border border-green-200', 'داخلي':'bg-blue-50 text-blue-600 border border-blue-200', 'سري':'bg-orange-50 text-orange-600 border border-orange-200', 'سري للغاية':'bg-red-50 text-red-600 border border-red-200' }
const TYPE_ICON  = { PDF:'📕', DOCX:'📘', XLSX:'📗', PPTX:'📙', ZIP:'📦' }

const FILTERS = [
  { key:'all',       label:'كل الملفات',          icon:'📄' },
  { key:'editing',   label:'أنا أحررها',           icon:'🔒' },
  { key:'others',    label:'يحررها آخرون',         icon:'✏️' },
  { key:'recent',    label:'محدثة مؤخراً',         icon:'🕐' },
  { key:'added',     label:'مضافة مؤخراً',         icon:'🆕' },
  { key:'favorites', label:'المفضلة',              icon:'⭐' },
]

const AREAS = [
  { key:'library',    label:'مكتبة المحتوى',  icon:'📚' },
  { key:'myfiles',   label:'ملفاتي',          icon:'🗂' },
  { key:'shared',    label:'ملفات مشتركة',    icon:'🤝' },
  { key:'repository',label:'المستودع',         icon:'🌐' },
]

export default function LibraryPage() {
  const { show, ToastContainer } = useToast()
  const [area, setArea]           = useState('library')
  const [view, setView]           = useState('detailed')
  const [sort, setSort]           = useState('modified')
  const [sortDir, setSortDir]     = useState('desc')
  const [filter, setFilter]       = useState('all')
  const [currentFolder, setCF]    = useState(null)
  const [folderPath, setFolderPath] = useState([])
  const [expanded, setExpanded]   = useState(new Set())
  const [selected, setSelected]   = useState(new Set())
  const [infoFile, setInfoFile]   = useState(null)
  const [search, setSearch]       = useState('')
  const [activeTags, setActiveTags] = useState(new Set())
  const [thumbSize, setThumbSize] = useState(130)
  const [showExplorer, setShowExplorer] = useState(true)
  const [showBread, setShowBread] = useState(true)
  const [files, setFiles]         = useState(MOCK_FILES)
  const [folders]                 = useState(MOCK_FOLDERS)
  const [dragOver, setDragOver]   = useState(false)
  const [apiLoading, setApiLoading] = useState(false)

  useEffect(() => {
    setApiLoading(true)
    client.get('/api/v1/documents', { params: { page:1, pageSize:50 } })
      .then(r => {
        const d = r.data?.data?.items || r.data?.data || r.data
        if (Array.isArray(d) && d.length > 0)
          setFiles(d.map(doc => ({
            id: doc.documentId || doc.id,
            name: doc.titleAr || doc.title,
            type: doc.fileType || 'PDF',
            size: doc.fileSize || '—',
            modified: doc.updatedAt ? new Date(doc.updatedAt).toLocaleDateString('ar-SA') : '—',
            created: doc.createdAt ? new Date(doc.createdAt).toLocaleDateString('ar-SA') : '—',
            version: doc.currentVersion || '1.0',
            owner: doc.ownerName || '—',
            folder: 'f1',
            tags: doc.tags || [],
            isFav: false, isCheckedOut: false, likes: 0, comments: 0,
            thumb: {PDF:'📕',DOCX:'📘',XLSX:'📗',PPTX:'📙',ZIP:'📦'}[doc.fileType] || '📄',
            classification: doc.classification || 'داخلي',
            status: doc.status || 'Active',
          })))
      })
      .catch(() => {}) // keep mock data
      .finally(() => setApiLoading(false))
  }, [])
  const [previewFile, setPreviewFile] = useState(null)
  const [shareFile, setShareFile]     = useState(null)

  const navigate = (folderId) => {
    setCF(folderId)
    setSelected(new Set())
    if (!folderId) { setFolderPath([]); return }
    const path = []
    let cur = folders.find(f => f.id === folderId)
    while (cur) { path.unshift(cur); cur = cur.parent ? folders.find(f => f.id === cur.parent) : null }
    setFolderPath(path)
  }

  const toggleSel = (id) => setSelected(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n })
  const toggleTag = (t) => setActiveTags(prev => { const n = new Set(prev); n.has(t) ? n.delete(t) : n.add(t); return n })
  const toggleExp = (id) => setExpanded(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n })
  const toggleFav = (file) => { setFiles(p => p.map(f => f.id===file.id ? {...f, isFav:!f.isFav} : f)); show(file.isFav?'إزالة من المفضلة':'إضافة للمفضلة','success') }

  // Merge API files with locally uploaded files (by id dedup)
  const allFiles = [
    ...(Array.isArray(libraryUploads) ? libraryUploads : []),
    ...files.filter(f => !(Array.isArray(libraryUploads) ? libraryUploads : []).find(u => u.id === f.id))
  ]
  const displayed = allFiles
    .filter(f => {
      if (area==='myfiles') return f.owner==='أحمد الزهراني'
      if (currentFolder)    return f.folder===currentFolder
      return true
    })
    .filter(f => {
      if (filter==='editing')   return f.isCheckedOut
      if (filter==='others')    return f.isCheckedOut && f.owner!=='أحمد الزهراني'
      if (filter==='recent')    return new Date(f.modified) > new Date(Date.now()-7*864e5)
      if (filter==='added')     return new Date(f.created)  > new Date(Date.now()-7*864e5)
      if (filter==='favorites') return f.isFav
      return true
    })
    .filter(f => {
      if (activeTags.size>0) return [...activeTags].every(t => f.tags.includes(t))
      if (search) return f.name.includes(search)||f.owner.includes(search)||f.tags.some(t=>t.includes(search))
      return true
    })
    .sort((a,b) => {
      const va = sort==='name'?a.name:sort==='size'?a.size:a[sort]||''
      const vb = sort==='name'?b.name:sort==='size'?b.size:b[sort]||''
      return sortDir==='asc' ? (va>vb?1:-1) : (va<vb?1:-1)
    })

  const subFolders = currentFolder
    ? folders.filter(f => f.parent===currentFolder)
    : folders.filter(f => !f.parent)

  const rootFolders = folders.filter(f => !f.parent)
  const childOf     = (id) => folders.filter(f => f.parent===id)

  const upload = () => {
    const i = document.createElement('input'); i.type='file'; i.multiple=true
    i.onchange = e => show(`تم رفع ${e.target.files.length} ملف`, 'success')
    i.click()
  }

  const bulkAction = (action) => { show(`${action} للملفات المحددة (${selected.size})`, 'success'); setSelected(new Set()) }

  return (
    <div className="flex flex-col -m-4 sm:-m-6" style={{height:'calc(100vh - 72px)'}}>
      <ToastContainer />
      {previewFile && <PreviewModal file={previewFile} onClose={()=>setPreviewFile(null)} />}
      {shareFile && <ShareModal file={shareFile} onClose={()=>setShareFile(null)} />}

      {/* Toolbar */}
      <div className="bg-white border-b border-gray-100 px-4 py-2 flex items-center gap-2 flex-wrap flex-shrink-0">
        <div className="flex gap-0.5 bg-gray-100 rounded-lg p-0.5">
          {AREAS.map(a => (
            <button key={a.key} onClick={()=>{setArea(a.key);setCF(null);setFolderPath([])}}
              className={`px-3 py-1.5 rounded-md text-xs font-medium transition-all whitespace-nowrap ${area===a.key?'bg-white shadow text-gray-900':'text-gray-500 hover:text-gray-700'}`}>
              {a.icon} {a.label}
            </button>
          ))}
        </div>
        <div className="flex-1"/>
        <div className="relative">
          <span className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-300 text-sm">🔍</span>
          <input value={search} onChange={e=>setSearch(e.target.value)} placeholder="بحث..."
            className="w-36 pr-8 pl-3 py-1.5 border border-gray-200 rounded-lg text-xs focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
        </div>
        <select value={sort} onChange={e=>setSort(e.target.value)} className="border border-gray-200 rounded-lg px-2 py-1.5 text-xs text-gray-600">
          <option value="modified">آخر تعديل</option>
          <option value="created">الإنشاء</option>
          <option value="name">الاسم</option>
          <option value="size">الحجم</option>
        </select>
        <button onClick={()=>setSortDir(d=>d==='asc'?'desc':'asc')} className="border border-gray-200 rounded-lg px-2 py-1.5 text-xs text-gray-600 hover:bg-gray-50">
          {sortDir==='asc'?'↑':'↓'}
        </button>
        <div className="flex border border-gray-200 rounded-lg overflow-hidden">
          {[['detailed','☰','تفصيلي'],['simple','≡','بسيط'],['gallery','⊞','معرض'],['filmstrip','▦','شريط']].map(([v,ic,lbl])=>(
            <button key={v} onClick={()=>setView(v)} title={lbl}
              className={`px-2.5 py-1.5 text-sm ${view===v?'bg-blue-600 text-white':'text-gray-500 hover:bg-gray-50'}`}>{ic}</button>
          ))}
        </div>
        {(view==='gallery'||view==='filmstrip')&&(
          <input type="range" min="80" max="200" value={thumbSize} onChange={e=>setThumbSize(+e.target.value)} className="w-16 accent-blue-600" title="حجم الصورة"/>
        )}
        <button onClick={()=>setShowExplorer(p=>!p)} className={`px-2 py-1.5 text-xs rounded-lg border ${showExplorer?'bg-gray-100 border-gray-300 text-gray-700':'border-gray-200 text-gray-400'}`} title="لوحة التنقل">🗂</button>
        <button onClick={upload} className="bg-blue-700 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-blue-800 flex items-center gap-1 shadow-sm">⬆️ رفع</button>
      </div>

      {/* Body */}
      <div className="flex flex-1 overflow-hidden">

        {/* Explorer */}
        {showExplorer && (
          <div className="w-52 bg-white border-l border-gray-100 flex flex-col overflow-hidden flex-shrink-0">
            <div className="p-3 border-b border-gray-50">
              <p className="text-[11px] font-bold text-gray-400 uppercase mb-2">التصفية السريعة</p>
              {FILTERS.map(f=>(
                <button key={f.key} onClick={()=>setFilter(f.key)}
                  className={`w-full flex items-center gap-2 text-xs px-2.5 py-1.5 rounded-lg mb-0.5 transition-colors ${filter===f.key?'bg-blue-100 text-blue-700 font-semibold':'text-gray-600 hover:bg-gray-50'}`}>
                  <span>{f.icon}</span><span>{f.label}</span>
                </button>
              ))}
            </div>

            {area==='library' && (
              <div className="p-3 border-b border-gray-50 overflow-y-auto flex-1">
                <p className="text-[11px] font-bold text-gray-400 uppercase mb-2">المجلدات</p>
                {rootFolders.map(folder => (
                  <div key={folder.id}>
                    <button onClick={()=>{navigate(folder.id);toggleExp(folder.id)}}
                      className={`w-full flex items-center gap-1.5 text-xs px-2 py-1.5 rounded-lg mb-0.5 transition-colors ${currentFolder===folder.id?'bg-blue-100 text-blue-700 font-semibold':'text-gray-600 hover:bg-gray-50'}`}>
                      <span className="text-[10px] text-gray-400">{childOf(folder.id).length>0?(expanded.has(folder.id)?'▼':'▶'):' '}</span>
                      <span>📁</span>
                      <span className="flex-1 truncate text-right">{folder.name}</span>
                      <span className="text-[10px] text-gray-400">{folder.count}</span>
                    </button>
                    {expanded.has(folder.id) && childOf(folder.id).map(child=>(
                      <button key={child.id} onClick={()=>navigate(child.id)}
                        className={`w-full flex items-center gap-1.5 text-xs pl-6 pr-2 py-1.5 rounded-lg mb-0.5 transition-colors ${currentFolder===child.id?'bg-blue-100 text-blue-700 font-semibold':'text-gray-500 hover:bg-gray-50'}`}>
                        <span>📂</span><span className="flex-1 truncate text-right">{child.name}</span>
                        <span className="text-[10px] text-gray-400">{child.count}</span>
                      </button>
                    ))}
                  </div>
                ))}
              </div>
            )}

            <div className="p-3">
              <p className="text-[11px] font-bold text-gray-400 uppercase mb-2">الوسوم</p>
              <div className="flex flex-wrap gap-1">
                {ALL_TAGS.map(tag=>(
                  <button key={tag} onClick={()=>toggleTag(tag)}
                    className={`text-[10px] px-1.5 py-0.5 rounded-full transition-colors ${activeTags.has(tag)?'bg-blue-600 text-white':'bg-gray-100 text-gray-500 hover:bg-gray-200'}`}>
                    #{tag}
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Content area */}
        <div className="flex-1 flex flex-col overflow-hidden">
          {showBread && (
            <div className="bg-white border-b border-gray-100 px-4 py-2 flex items-center gap-2 flex-shrink-0">
              <button onClick={()=>navigate(null)} className="text-xs text-blue-600 hover:underline font-medium">🏠 المكتبة</button>
              {folderPath.map((p,i)=>(
                <React.Fragment key={p.id}>
                  <span className="text-gray-300 text-xs">/</span>
                  <button onClick={()=>navigate(p.id)} className={`text-xs ${i===folderPath.length-1?'text-gray-800 font-semibold':'text-blue-600 hover:underline'}`}>{p.name}</button>
                </React.Fragment>
              ))}
              {folderPath.length>0 && <button onClick={()=>navigate(folderPath[folderPath.length-2]?.id||null)} className="text-xs text-gray-400 hover:text-gray-600 mr-2">↑ للأعلى</button>}
              <button onClick={()=>setShowBread(false)} className="text-gray-300 hover:text-gray-500 text-xs mr-auto">✕</button>
            </div>
          )}

          {/* Bulk actions */}
          {selected.size>0 && (
            <div className="bg-blue-600 text-white px-4 py-2 flex items-center gap-3 flex-shrink-0">
              <span className="text-sm font-bold">{selected.size} محدد</span>
              {['⬇️ تنزيل','🔗 مشاركة','📤 إرسال للاعتماد','🗑️ حذف'].map(a=>(
                <button key={a} onClick={()=>bulkAction(a)} className="text-xs bg-white/20 hover:bg-white/30 px-3 py-1 rounded-lg">{a}</button>
              ))}
              <button onClick={()=>setSelected(new Set())} className="mr-auto text-white/70 hover:text-white text-sm">✕</button>
            </div>
          )}

          {/* Files list */}
          <div
            onDragOver={e=>{e.preventDefault();setDragOver(true)}}
            onDragLeave={()=>setDragOver(false)}
            onDrop={e=>{e.preventDefault();setDragOver(false);show(`تم رفع ${e.dataTransfer.files.length} ملف`,'success')}}
            className={`flex-1 overflow-auto p-4 transition-all ${dragOver?'bg-blue-50 ring-2 ring-inset ring-blue-400':''}`}>

            {/* Subfolders */}
            {subFolders.length>0 && area==='library' && (
              <div className="mb-5">
                <p className="text-xs font-bold text-gray-400 mb-2">📁 المجلدات ({subFolders.length})</p>
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6 gap-2 mb-4">
                  {subFolders.map(f=>(
                    <div key={f.id} onDoubleClick={()=>navigate(f.id)}
                      className="bg-white border border-gray-200 rounded-xl p-3 cursor-pointer hover:border-blue-300 hover:shadow-sm transition-all text-center">
                      <div className="text-3xl mb-1">📁</div>
                      <p className="text-xs font-medium text-gray-700 truncate">{f.name}</p>
                      <p className="text-[10px] text-gray-400">{f.count} ملف</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Files */}
            <p className="text-xs font-bold text-gray-400 mb-2">📄 الملفات ({displayed.length})</p>

            {displayed.length===0 && (
              <div className="text-center py-20">
                <div className="text-4xl mb-3 opacity-30">📭</div>
                <p className="text-sm text-gray-400">لا توجد ملفات</p>
                <p className="text-xs text-gray-300 mt-1">ارفع ملفات أو غيّر التصفية</p>
              </div>
            )}

            {/* Gallery view */}
            {(view==='gallery'||view==='filmstrip') && (
              <div className={`grid gap-3 ${view==='gallery'?'grid-cols-2 sm:grid-cols-3 lg:grid-cols-4':'grid-cols-3 sm:grid-cols-4 lg:grid-cols-6'}`}>
                {displayed.map(f=>(
                  <div key={f.id} onClick={()=>toggleSel(f.id)}
                    className={`bg-white rounded-xl border-2 overflow-hidden cursor-pointer transition-all group ${selected.has(f.id)?'border-blue-500 shadow-md':'border-gray-100 hover:border-blue-300'}`}>
                    <div className={`${view==='filmstrip'?'h-20':'h-32'} bg-gradient-to-br from-gray-50 to-gray-100 flex items-center justify-center relative`}
                      style={{minHeight: view==='gallery'?thumbSize:80}}>
                      <span className="text-5xl opacity-40">{f.thumb}</span>
                      <div className="absolute top-1.5 right-1.5">
                        <input type="checkbox" checked={selected.has(f.id)} onChange={()=>toggleSel(f.id)} onClick={e=>e.stopPropagation()} className="w-3.5 h-3.5 accent-blue-600 rounded"/>
                      </div>
                      {f.isCheckedOut && <span className="absolute top-1.5 left-1.5 bg-yellow-400 text-white text-[9px] px-1 rounded font-bold">🔒</span>}
                      <div className="absolute inset-0 bg-black/0 group-hover:bg-black/5 transition-all flex items-end justify-center gap-1 pb-2 opacity-0 group-hover:opacity-100">
                        <button onClick={e=>{e.stopPropagation();setPreviewFile(f)}}  className="bg-white shadow text-xs px-2 py-1 rounded-lg">👁</button>
                        <button onClick={e=>{e.stopPropagation();setInfoFile(f)}}                     className="bg-white shadow text-xs px-2 py-1 rounded-lg">ℹ</button>
                      </div>
                    </div>
                    <div className="p-2">
                      <p className="text-xs font-semibold text-gray-800 truncate">{f.name}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <span className="text-[10px] text-gray-400">v{f.version}</span>
                        {f.isFav && <span className="text-yellow-400 text-xs">⭐</span>}
                        <span className="text-[10px] text-gray-400 mr-auto">❤️{f.likes}</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* Simple view */}
            {view==='simple' && (
              <div className="bg-white rounded-xl border border-gray-100 overflow-hidden">
                {displayed.map((f,i)=>(
                  <div key={f.id} onClick={()=>toggleSel(f.id)}
                    className={`flex items-center gap-3 px-3 py-2 cursor-pointer transition-colors group ${i>0?'border-t border-gray-50':''} ${selected.has(f.id)?'bg-blue-50':'hover:bg-gray-50'}`}>
                    <input type="checkbox" checked={selected.has(f.id)} onChange={()=>toggleSel(f.id)} onClick={e=>e.stopPropagation()} className="w-4 h-4 accent-blue-600 rounded flex-shrink-0"/>
                    <span className="text-lg flex-shrink-0">{TYPE_ICON[f.type]||'📄'}</span>
                    <span className="flex-1 text-sm text-gray-800 truncate">{f.name}</span>
                    {f.isFav && <span className="text-yellow-400 text-xs">⭐</span>}
                    <span className="text-xs text-gray-400">{f.size}</span>
                    <div className="opacity-0 group-hover:opacity-100 flex gap-1">
                      <button onClick={e=>{e.stopPropagation();setPreviewFile(f)}} className="p-1 rounded hover:bg-gray-200 text-gray-500 text-sm">👁</button>
                      <button onClick={e=>{e.stopPropagation();setInfoFile(f)}}                    className="p-1 rounded hover:bg-gray-200 text-gray-500 text-sm">ℹ</button>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* Detailed view */}
            {view==='detailed' && (
              <div className="space-y-1">
                {displayed.map(f=>(
                  <div key={f.id} onClick={()=>toggleSel(f.id)}
                    className={`flex items-center gap-3 px-3 py-2.5 rounded-xl cursor-pointer group border-2 transition-all ${selected.has(f.id)?'border-blue-300 bg-blue-50':'border-transparent hover:border-gray-200 hover:bg-gray-50/70'}`}>
                    <input type="checkbox" checked={selected.has(f.id)} onChange={()=>toggleSel(f.id)} onClick={e=>e.stopPropagation()} className="w-4 h-4 accent-blue-600 rounded flex-shrink-0"/>
                    <span className="text-2xl flex-shrink-0">{TYPE_ICON[f.type]||'📄'}</span>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span className="text-sm font-medium text-gray-800 truncate">{f.name}</span>
                        {f.isCheckedOut && <span className="text-[10px] bg-yellow-100 text-yellow-700 px-1.5 py-0.5 rounded-full font-medium flex-shrink-0">🔒 محجوز</span>}
                        {f.isFav && <span className="text-yellow-400 text-xs">⭐</span>}
                      </div>
                      <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                        <span className="text-[10px] text-gray-400">{f.owner}</span>
                        <span className="text-gray-200 text-[10px]">•</span>
                        <span className="text-[10px] text-gray-400">v{f.version}</span>
                        {f.tags.map(t=><span key={t} className="text-[10px] bg-gray-100 text-gray-400 px-1.5 py-0.5 rounded-full">#{t}</span>)}
                      </div>
                    </div>
                    <div className="flex items-center gap-2 flex-shrink-0">
                      <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium hidden sm:block ${STATUS_CLS[f.status]}`}>{STATUS_LBL[f.status]}</span>
                      <span className={`text-[10px] px-1.5 py-0.5 rounded font-medium hidden lg:block ${CLASS_CLS[f.classification]}`}>{f.classification}</span>
                      <span className="text-xs text-gray-400 w-14 text-left hidden md:block">{f.size}</span>
                      <span className="text-xs text-gray-400 hidden xl:block">{f.modified}</span>
                      <div className="opacity-0 group-hover:opacity-100 flex gap-1 transition-opacity">
                        <button onClick={e=>{e.stopPropagation();setPreviewFile(f)}}    title="معاينة"  className="p-1.5 rounded-lg hover:bg-gray-200 text-sm">👁</button>
                        <button onClick={e=>{e.stopPropagation();show(`تنزيل: ${f.name}`,'info')}}     title="تنزيل"   className="p-1.5 rounded-lg hover:bg-gray-200 text-sm">⬇</button>
                        <button onClick={e=>{e.stopPropagation();toggleFav(f)}}                        title="مفضلة"   className="p-1.5 rounded-lg hover:bg-gray-200 text-sm">{f.isFav?'⭐':'☆'}</button>
                        <button onClick={e=>{e.stopPropagation();setInfoFile(f)}}                      title="تفاصيل"  className="p-1.5 rounded-lg hover:bg-gray-200 text-sm">ℹ</button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {!showBread && (
              <button onClick={()=>setShowBread(true)} className="fixed bottom-4 left-4 bg-white shadow-lg border border-gray-200 text-xs text-blue-600 px-3 py-1.5 rounded-full hover:shadow-xl transition-shadow">
                إظهار مسار التنقل
              </button>
            )}
          </div>
        </div>

        {/* Info panel */}
        {infoFile && (
          <div className="w-64 bg-white border-r border-gray-100 flex flex-col overflow-hidden flex-shrink-0">
            <div className="p-4 border-b border-gray-100 flex items-center justify-between">
              <h3 className="font-bold text-sm">معلومات الملف</h3>
              <button onClick={()=>setInfoFile(null)} className="text-gray-400 hover:text-gray-600">✕</button>
            </div>
            <div className="flex-1 overflow-y-auto p-4 space-y-4">
              <div className="h-28 bg-gradient-to-br from-gray-50 to-gray-100 rounded-xl flex items-center justify-center">
                <span className="text-5xl opacity-50">{infoFile.thumb}</span>
              </div>
              <div className="flex gap-2">
                <button onClick={()=>toggleFav(infoFile)}
                  className={`flex-1 py-1.5 text-xs font-medium rounded-lg border transition-colors ${infoFile.isFav?'bg-yellow-50 border-yellow-300 text-yellow-700':'border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
                  {infoFile.isFav?'⭐ مفضل':'☆ إضافة'}
                </button>
                <button onClick={()=>setFiles(p=>p.map(f=>f.id===infoFile.id?{...f,likes:f.likes+1}:f))}
                  className="flex-1 py-1.5 text-xs font-medium rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 transition-colors">
                  ❤️ {infoFile.likes}
                </button>
              </div>
              <div className="bg-gray-50 rounded-xl p-3 space-y-2">
                {[['الاسم',infoFile.name],['النوع',infoFile.type],['الحجم',infoFile.size],['الإصدار',`v${infoFile.version}`],['المالك',infoFile.owner],['آخر تعديل',infoFile.modified],['الإنشاء',infoFile.created],['التصنيف',infoFile.classification],['الحالة',STATUS_LBL[infoFile.status]]].map(([k,v])=>(
                  <div key={k} className="flex justify-between text-xs">
                    <span className="text-gray-400">{k}</span>
                    <span className="font-medium text-gray-700 truncate max-w-[110px] text-right">{v}</span>
                  </div>
                ))}
              </div>
              {infoFile.tags.length>0 && (
                <div>
                  <p className="text-xs font-semibold text-gray-400 mb-1.5">الوسوم</p>
                  <div className="flex flex-wrap gap-1">{infoFile.tags.map(t=><span key={t} className="bg-blue-50 text-blue-600 text-[11px] px-2 py-0.5 rounded-full">#{t}</span>)}</div>
                </div>
              )}
              <div className="space-y-1.5 pt-2 border-t border-gray-100">
                <p className="text-xs font-semibold text-gray-400 mb-2">إجراءات</p>
                {[['👁️ معاينة'],['⬇️ تنزيل'],['✏️ تعديل البيانات'],['🔒 استعارة'],['📤 إرسال للاعتماد'],['🔗 مشاركة'],['🗑️ حذف']].map(([a])=>(
                  <button key={a} onClick={()=>show(a,'info')}
                    onClick={()=>{
                      if(a.includes('معاينة')) setPreviewFile(infoFile)
                      else if(a.includes('مشاركة')) setShareFile(infoFile)
                      else show(a,'info')
                    }}
                    className={`w-full text-right text-xs px-3 py-2 rounded-lg border transition-colors ${a.includes('حذف')?'border-red-100 text-red-500 hover:bg-red-50':a.includes('معاينة')?'border-blue-100 text-blue-600 hover:bg-blue-50':'border-gray-100 text-gray-600 hover:bg-gray-50'}`}>
                    {a}
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
